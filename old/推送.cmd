@echo off
echo "xbz" >> README.md
git init
git add .gitignore
git commit -m "Add .gitignore file"
git branch -M main1
::git add README.md
git remote add origin https://github.com/m4a1ls/BatchCompression.git
<<<<<<< Updated upstream
git push -u origin main1
=======
git push -u origin main
>>>>>>> Stashed changes
pause