#!/bin/bash
shared_dir=/Users/korhun/vm_shared/pack/
cd ..
clear

rm -rf $shared_dir

sh ./pack.sh

mkdir  $shared_dir
cp pack/* $shared_dir
