sudo for Windows
================

[![Build status](https://ci.appveyor.com/api/projects/status/2d880hsxvy2levdm/branch/master?svg=true)](https://ci.appveyor.com/project/wzv5/sudo/branch/master)

## 特性

* 重定向输入输出到当前控制台窗口，如Linux般顺滑体验
* 自动检测目标程序是否为GUI PE，如果是则以普通方式运行
* 自动检测当前用户是否位于 Administrators 用户组

## 下载
[sudo.exe](https://github.com/wzv5/sudo/releases/latest)

## 使用方法

`
sudo <cmd...>
`

## 感谢

* <https://raw.githubusercontent.com/lukesampson/psutils/efcd212cf7/sudo.ps1>
* <https://stackoverflow.com/questions/11668026/get-path-to-executable-from-command-as-cmd-does>

## 开源协议

[MIT](http://opensource.org/licenses/MIT)