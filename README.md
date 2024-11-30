[![CI/CD](https://github.com/dahln/darknote/actions/workflows/dotnet-build-deploy.yml/badge.svg)](https://github.com/dahln/darknote/actions/workflows/dotnet-build-deploy.yml)

## What & Why
Darknote is a SaaS, open-source, user-friendly, minimal notes app. It allows users to create notes and lists and share notes and lists with other people. 

Because this project is open-source, you can deploy this application how and where you want. You control the data.


## Demo
[https://darknote.org#HowTo](https://darknote.org#HowTo)


## Technologies
 - .NET & C#
 - Web API
 - Blazor WASM
 - Web API
 - SQL (Sqlite)
 - Identity API
 - Identity API 2FA
 - [Blazored Libraries (Toast, LocalStorage, Modal)](https://github.com/Blazored)
 - [Bootstrap CDN (Bootstrap and Boostrap Icons)](https://getbootstrap.com/)

## Getting Started
You can run this project locally.

Requirements: You must have the [ASP.NET Core Runtime](https://dotnet.microsoft.com/en-us/download) installed.
1. Navigate to the project root folder
2. run this command: "dotnet watch --project .\Darknote.API\"
3. This command will startup the application and create the required Sqlite database file.
4. Register an account and sign-in.

## Deployment Instructions
A tutorial on deploying is coming soon.

## [SendGrid](https://sendgrid.com/en-us/pricing)
This project uses SendGrid to send emails. A SendGrid API key is required. You will need to specify your own SendGrid API key and system email address in the admin settings page. Some features that require email are not available until you provide the necessary SendGrid values. It is a simple process to create your own SendGrid account and retreive your API key.

## Why SQLite?
It runs on Windows and Linux. Moving and controlling your data is easy. If you need more than SQLite offers then I recommend switching to Azure SQL. If you switch to Azure SQL, besure to delete your SQLite DB migrations and create new a 'Initial Migration' for your new Azure SQL DB.

## Licensing
Refer to the LICENSE file


