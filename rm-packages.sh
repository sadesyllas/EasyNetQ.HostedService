#!/bin/bash

pushd "$(dirname "$0")"

find . -name '*.nupkg' | xargs rm -fv

popd
