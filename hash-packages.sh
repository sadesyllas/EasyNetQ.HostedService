#!/bin/bash

pushd "$(dirname "$0")"

find . -name '*.nupkg' | while read p; do
    echo -n "SHA512 $(basename "$p") "
    sha512sum "$p" | gawk '{print $1}'
done > package-hashes.txt

popd
