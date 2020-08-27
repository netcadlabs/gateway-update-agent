#!/bin/bash
clear

rm -rf bin obj pack
dotnet restore
dotnet deb -c Release -r linux-x64
# dotnet deb -c Release -r linux-arm
# dotnet deb -c Release -r linux-arm64

mkdir ./pack/
cp ./bin/Release/netcoreapp3.1/linux-x64/GatewayUpdateAgent*linux-x64.deb ./pack/ 2>/dev/null
cp ./bin/Release/netcoreapp3.1/linux-arm/GatewayUpdateAgent*linux-arm.deb ./pack/ 2>/dev/null
cp ./bin/Release/netcoreapp3.1/linux-arm64/GatewayUpdateAgent*linux-arm64.deb ./pack/ 2>/dev/null
