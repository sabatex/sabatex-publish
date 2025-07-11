# Публікація (деплой) проекта
  - на NUGET або локальний репозиторій пакетів
  - ASP.NET проекту linux сервер

## Інсталяція

Скопіювати sabatex-tools.exe в папку до якої описано шлях в змінних середовища PATH.

## Використання

На командному рядку перейдіть до папки, де знаходиться файл *.csproj. І запустіть sabatex-publish.exe, в залежності від типу, проект буде опубліковано в NUGET або на linux сервер.
Параметри командного рядка:
- --csproj{шлях до файлу csproj} увага ! шлях повинен бути без пробілів і починатися з кореня диска наприклад --csprojc:\projects\myproject\myproject.csproj
- --migrate - після публікації проекту на linux сервер, виконується міграція бази даних
- --updateservice - після публікації проекту на linux сервер, оновлюється служба в системі linux або реєструється нова служба для даного проекту
- --updatenginx - після публікації проекту на linux сервер, оновлюється конфігурація NGINX для даного проекту або реєструється новий сайт в NGINX

### Публікація та налаштування для публікації (деплою) проекту в NUGET
В папці де знаходиться файл sabatex-publish.exe створити файл sabatex-publish.json з наступним вмістом:
```json
{
  "SabatexSettings": {
    "NUGET": {
      "nugetAuthTokenPath": "шлях до файлу з NUGET токеном",
      "LocalDebugStorage": "локальна папка для зберігання пакетів"
    }
  }
}
```
Це налаштування використовується для всіх ваших проектів.
<b>Зверніть увагу на місце зберігання файлу з токеном, він повинен бути в безпечному місці.</b>
NUGET токен можна отримати на сайті NUGET.
NUGET токен потрібен для публікації пакетів в репозиторій NUGET.
NUGET токен це текстовий файл з токеном, який ви отримали на сайті NUGET.

Увага ! В файлі ваш-проект.csproj повинен бути вказаний наступний тег:
- приклад для релізної версії проекту
```xml
<Version>1.0.0</Version>
```
- приклад для версії проекту в режимі налагодження
```xml
<Version>1.0.0-rc1</Version>
```

Релізна версія публікується на сайт NUGET, версія в режимі налагодження публікується в локальний репозиторій.


### Публікація та налаштування для публікації (деплою) проекту на linux сервер

В файлі appsettings.json або в usersecrets додайте наступну секцію:
```json
{
  "SabatexSettings": {
     "Linux": {
        "ServiceName":"необовязково назва служби в ubuntu(за замовчуванням назва служби як назва проекту)",
        "UserHomeFolder":"обовязково шлях до папки користувача в linux, наприклад /home/azureuser",
        "Port":"необовязково TCP порт asp додатку, за замовчуванням 5000",
        "FrontEnd":"необовязково True - standalone Blazor WASM, false - asp.net core проект (за замовчуванням false)",
        "PublishFolder":"необовязково шлях до папки в якій буде розміщено проект (за замовчуванням /var/www/{назва проекта})",
        "BitviseTlpFile":"обовязково шлях до файлу Bitvise tlp який збережений з налаштованим зэднанням до вашого linux",
        "NGINX":{
            "SSLPublic": "необовязково шлях до публічного сертифікату SSL",
            "SSLPrivate": "необовязково шлях до приватного сертифікату SSL",
            "HostName":["необовязково ваше ім'я хоста (contoso.com)","www.contoso.com"]
        }
    }
  }
}
```
