# GATool API

![gatool-api deploy status](https://github.com/arthurlockman/gatool-api/actions/workflows/main_gatool-api.yml/badge.svg)

gatool is a tool to provide FIRST® Game Announcers with up to date information while announcing events during the FIRST Robotics season. As a web-based tool, it uses up-to-date information about the event to provide a comprehensive set of useful data to Game Announcers. It is designed to work on desktops, laptops and tablet devices. In a pinch, it can be used on a mobile phone.

You will need a login to access the tool. All registered GAs and MCs will receive an invitation with a login and password.

[Watch an overview of gatool on YouTube to learn more!](https://youtu.be/-n96KgtgYF0)

## Contributions

We welcome any and all contributions! Please feel free to fork the repository and contribute back to our development. [Issues can be filed in the GitHub issue tracker](https://github.com/arthurlockman/gatool-api/issues/new).

## Development

This section has information on how to build and deploy the project.

### Building

GAtool's backend is built on nodejs. To run the application, run:

```bash
git clone git@github.com:arthurlockman/gatool-api.git
cd gatool-api
npm i
npm run start:no-redis
```

#### Configuring local development environment

First configure gatool-ui according to the instructions for local development, including Auth0 setup. Then, install [Azurite](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite). Start the Azurite blob store according to Microsoft's documentation. (If using VS Code, ensure you set the working directory in the extension settings to a directory outside of the repository or you will end up with a dirty repo.)

Run `npm run create-dev-containers` to create the necessary containers in the blob store.

Copy `.env.example` to `.env`
Fill in the following variables:
- Auth0Issuer with the domain used when setting up gatool-ui
- Auth0Audience with the client id from setting up gatool-ui
- FRCApiKey with the basic auth header (`Basic <base64(username:password)>`) for a [FRC API username/key](https://frc-events.firstinspires.org/services/api/register)
- TBAApiKey with a [TBA Read API key](https://www.thebluealliance.com/account)

Start the server using `npm run start:env` (or `npm run watch:env`)

### Deployment

GAtool is deployed on Azure. We are using Azure Block Storage for user uploads, and Azure VMs to host the service itself. We deploy using [PM2](https://pm2.keymetrics.io) to our hosts.
