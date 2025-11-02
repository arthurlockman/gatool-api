# GATool API

![gatool-api deploy status](https://github.com/arthurlockman/gatool-api/actions/workflows/main_gatool-api.yml/badge.svg)

gatool is a tool to provide **_FIRST®_** Game Announcers with up to date information while announcing events during the
**_FIRST_** Robotics season. As a web-based tool, it uses up-to-date information about the event to provide a
comprehensive set of useful data to Game Announcers. It is designed to work on desktops, laptops and tablet devices. In
a pinch, it can be used on a mobile phone.

gatool relies on data from APIs provided by [**_FIRST_
**](https://frc-api-docs.firstinspires.org), [The Blue Alliance](https://www.thebluealliance.com/apidocs/v3), [statbotics.io](https://www.statbotics.io/docs/rest),
and [FTCScout](https://ftcscout.org/api) as well as its own data sources. We created the gatool APIs to provide a single access point for the gatool and for
other tools that want to use its data. We ask that if you build a tool that uses gatool APIs, that you include "Data
provided by the **_FIRST_** Game Announcer Tool API" and a link to this page in your application/service.

[Watch an overview of gatool on YouTube to learn more!](https://youtu.be/-n96KgtgYF0)

## Contributions

We welcome any and all contributions! Please feel free to fork the repository and contribute back to our
development. [Issues can be filed in the GitHub issue tracker](https://github.com/arthurlockman/gatool-api/issues/new).

## Development

TODO: rewrite new development section

## API Documentation

All endpoints are served from:

```
https://api.gatool.org/
```

### 🚀 NEW: All API documentation (including types!) is auto-generated and available at https://api.gatool.org/swagger/
