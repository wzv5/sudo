using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Diagnostics;
using System.Runtime.Serialization.Formatters.Binary;

// https://raw.githubusercontent.com/lukesampson/psutils/efcd212cf7/sudo.ps1

namespace sudo
{
    class Program
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        public static extern bool FreeConsole();

        public static bool IsInAdminGroup()
        {
            var identity = WindowsIdentity.GetCurrent();
            return identity.Claims.Any(g => g.Value == "S-1-5-32-544");
        }

        public static bool IsGuiExe(string exepath)
        {
            using (var s = File.OpenRead(exepath))
            {
                using (var r = new BinaryReader(s))
                {
                    if (r.ReadByte() != 0x4d && r.ReadByte() != 0x5a)
                    {
                        return false;
                    }
                    s.Seek(0x3c, SeekOrigin.Begin);
                    var e_lfanew = r.ReadUInt32();
                    s.Seek(e_lfanew + 0x5c, SeekOrigin.Begin);
                    var subsystem = r.ReadUInt16();
                    return subsystem == 2;  // IMAGE_SUBSYSTEM_WINDOWS_GUI
                }
            }
        }

        // https://stackoverflow.com/questions/11668026/get-path-to-executable-from-command-as-cmd-does
        public static string WhereSearch(string filename)
        {
            var paths = new[] { Environment.CurrentDirectory }
                    .Concat(Environment.GetEnvironmentVariable("PATH").Split(';'));
            var extensions = new[] { String.Empty }
                    .Concat(Environment.GetEnvironmentVariable("PATHEXT").ToLower().Split(';')
                               .Where(e => e.StartsWith(".")));
            var combinations = paths.SelectMany(x => extensions,
                    (path, extension) => Path.Combine(path, filename + extension));
            return combinations.FirstOrDefault(File.Exists);
        }

        public static string Serialize(string[] args)
        {
            var formatter = new BinaryFormatter();
            using (var s = new MemoryStream())
            {
                formatter.Serialize(s, args);
                using (var r = new BinaryReader(s))
                {
                    s.Seek(0, SeekOrigin.Begin);
                    var buf = r.ReadBytes((int)s.Length);
                    return Convert.ToBase64String(buf);
                }
            }
        }

        public static string[] Deserialize(string base64args)
        {
            var formatter = new BinaryFormatter();
            var buf = Convert.FromBase64String(base64args);
            using (var s = new MemoryStream())
            {
                s.Write(buf, 0, buf.Length);
                s.Seek(0, SeekOrigin.Begin);
                return formatter.Deserialize(s) as string[];
            }
        }

        public static string ConcatArgs(string[] args)
        {
            var sb = new StringBuilder();
            foreach (var a in args)
            {
                var aa = a.Replace("\"", "\"\"");
                if (a.Any(c => c == ' '))
                {
                    sb.Append("\"" + aa + "\" ");
                }
                else
                {
                    sb.Append(aa + " ");
                }
            }
            return sb.ToString().Trim();
        }

        public static int UserMain(string[] args)
        {
            if (!IsInAdminGroup())
            {
                Console.Error.WriteLine("you must be an administrator to run sudo");
                return 1;
            }
            var fullpath = WhereSearch(args[0]);
            //Console.WriteLine("* fullpath：[" + fullpath + "]");
            if (!File.Exists(fullpath))
            {
                Console.Error.WriteLine("cannot find specific file");
                return 1;
            }
            if (IsGuiExe(fullpath))
            {
                var p = new Process();
                var si = p.StartInfo;
                si.FileName = fullpath;
                si.Arguments = ConcatArgs(args.Skip(1).ToArray());
                si.Verb = "runas";
                try
                {
                    p.Start();
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e.Message);
                    return 1;
                }
                return 0;
            }
            else
            {
                var p = new Process();
                var si = p.StartInfo;
                si.FileName = Process.GetCurrentProcess().MainModule.FileName;
                si.Arguments = string.Format("-a {0} {1}", Process.GetCurrentProcess().Id, Serialize(new[] { fullpath }.Concat(args.Skip(1)).ToArray()));
                si.Verb = "runas";
                si.WindowStyle = ProcessWindowStyle.Hidden;
                try
                {
                    p.Start();
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e.Message);
                    return 1;
                }
                p.WaitForExit();
                return p.ExitCode;
            }
        }

        // sudo.exe -a [parent pid] [serialized data]
        public static int AdminMain(string[] args)
        {
            FreeConsole();
            AttachConsole(uint.Parse(args[1]));

            var a = Deserialize(args[2]);
            var p = new Process();
            var si = p.StartInfo;
            si.FileName = a[0];
            si.Arguments = ConcatArgs(a.Skip(1).ToArray());
            si.WindowStyle = ProcessWindowStyle.Hidden;
            si.UseShellExecute = false;
            p.Start();
            p.WaitForExit();
            return p.ExitCode;
        }

        public static int PrintHelp(string[] args)
        {
            Console.WriteLine("usage: sudo <cmd...>");
            return 0;
        }

        static int Main(string[] args)
        {
            if (args.Length >= 3 && args[0] == "-a")
            {
                return AdminMain(args);
            }
            else if (args.Length > 0)
            {
                return UserMain(args);
            }
            else
            {
                return PrintHelp(args);
            }
        }
    }
}
