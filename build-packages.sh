#!/bin/bash

pushd "$(dirname "$0")"

bash rm-packages.sh

dotnet pack -c:Release --force

popd
