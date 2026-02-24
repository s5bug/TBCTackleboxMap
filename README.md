# TBCTackleboxMap

Shows a map with progress for each location
- on the pause screen, toggled by pressing Lockon
- in-game, enabled by holding Tether and Interact

![Screenshot of pause screen with Map enabled](https://elixi.re/i/76kav.png)

## development

Edit `TBCTackleboxMap.csproj.user` correcting the path to your The Big Catch Tacklebox folder. An example
with Windows Steam on the C drive is shown below:

```msbuild
<Project>
    <PropertyGroup>
        <TbcTackleboxPath>C:/Program Files (x86)/Steam/steamapps/common/The Big Catch Tacklebox</TbcTackleboxPath>
    </PropertyGroup>
</Project>
```
