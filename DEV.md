# Dev Notes

**Tagger la release**

`git tag v?.?.?`

`git push origin v?.?.?` ou `git push --tags`

**Construction manuelle de la release**

`dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishReadyToRun=true -p:EnableCompressionInSingleFile=true`

**Publier la release**

Créer une archive de l'exécutable puis publier ici:

https://github.com/Ace4teaM/animate/releases/new

