#!/bin/bash

pushd "$(dirname "$0")"

find . -name '*.nupkg' | grep -i '\/release\/' | while read p; do
    dotnet nuget push "$p" -s https://api.nuget.org/v3/index.json -k "$1" --skip-duplicate
done

popd
