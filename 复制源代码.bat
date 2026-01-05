@echo off
chcp 65001 >nul
echo 正在复制源代码文件...

cd /d "%~dp0\.."

copy /Y *.cs "开源协议上传完整代码\" >nul
copy /Y *.csproj "开源协议上传完整代码\" >nul
copy /Y *.sln "开源协议上传完整代码\" >nul
copy /Y *.md "开源协议上传完整代码\" >nul 2>nul
copy /Y *.txt "开源协议上传完整代码\" >nul 2>nul

echo 复制完成！
echo.
echo 请检查"开源协议上传完整代码"目录中的文件
pause

