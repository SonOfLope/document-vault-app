#!/bin/bash

for py_file in *.py; do
    if [ -f "$py_file" ]; then
        echo "Running $py_file..."
        python3 "$py_file"
    fi
done

echo "diagrams generated"