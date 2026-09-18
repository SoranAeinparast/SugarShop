@echo off
rem Detached test launcher for live verification (port 5111, shadow output)
set ASPNETCORE_ENVIRONMENT=Development
set ASPNETCORE_URLS=http://127.0.0.1:5111
cd /d "C:\+Programs\SUGARSHOP_FOR GAPGPT\++SugarShop++\SugarShop.Web"
dotnet "C:\+Programs\SUGARSHOP_FOR GAPGPT\++SugarShop++\.freebuff\shadow-bin\SugarShop.Web.dll" > "C:\+Programs\SUGARSHOP_FOR GAPGPT\++SugarShop++\.freebuff\appdl-test.log" 2>&1
