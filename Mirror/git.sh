#!/bin/bash

if [ -z "$1" ]
then
# 인자가 없는 경우
git add .
echo "git add done!"

git commit -m "commit"
echo "git commit done!"

git push origin mirror
echo "git push done!"

else
# 인자가 있는 경우
git add .
echo "git add done!"

git commit -m "$1"
echo "git commit -m $1"

git push origin mirror
echo "git push done!"
fi

# usage: ./git.sh
# usage: sh git.sh
# usage with argument : ./git.sh "commit message"

# explain : 위 코드에서는 if 문을 사용하여 $1이 비어있는지 확인합니다. 
# $1은 첫 번째 실행 인자를 나타내며, 비어있는 경우 -z 옵션을 사용하여 참인지 거짓인지 확인합니다. 
# 인자가 없는 경우에는 "hi"라는 메시지로 커밋하고, 인자가 있는 경우에는 $1에 전달된 메시지로 커밋합니다. 
# 이후, git push 명령어를 실행하여 변경 내용을 원격 저장소에 푸시합니다. 
# 위 코드에서는 if 문을 사용하여 인자의 유무에 따라 다르게 실행하고 있습니다. 
# 이를 통해, 쉘 스크립트를 보다 유연하게 작성할 수 있습니다.
