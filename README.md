# GATool API

![gatool-api deploy status](https://github.com/arthurlockman/gatool-api/actions/workflows/main_gatool-api.yml/badge.svg)

gatool is a tool to provide **_FIRST®_** Game Announcers with up to date information while announcing events during the **_FIRST_** Robotics season. As a web-based tool, it uses up-to-date information about the event to provide a comprehensive set of useful data to Game Announcers. It is designed to work on desktops, laptops and tablet devices. In a pinch, it can be used on a mobile phone.

gatool relies on data from APIs provided by [**_FIRST_**](https://frc-api-docs.firstinspires.org), [The Blue Alliance](https://www.thebluealliance.com/apidocs/v3), [statbotics.io](https://www.statbotics.io/docs/rest), as well as its own data sources. We created the gatool APIs to provide a single access point for the gatool and for other tools that want to use its data. We ask that if you build a tool that uses gatool APIs, that you include "Data provided by the **_FIRST_** Game Announcer Tool API" and a link to this page in your application/service.

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

# GATool Endpoint Documentation

## Base URL

All endpoints are served from:

```
https://api.gatool.org/
```

---

## v3 API Endpoints

### Teams

- **GET `/v3/:year/teams?{queryParameter}=`**

  - Query Parameters: `eventCode`, `districtCode`, `teamNumber` (requires one of these parameters)
  - Returns: List of teams for a given year, optionally filtered.
  - **Response Object:**
    ```json
    {
      "teamCountTotal": 36,
      "teamCountPage": 36,
      "pageCurrent": 1,
      "pageTotal": 1,
      "teams": [
        {
          "teamNumber": 97,
          "nameFull": "Cambridge Rindge and Latin School/Gene Haas Foundation/Rindge School of Technical Arts/Analog Devices/Google&Cambridge Rindge & Latin HS",
          "nameShort": "Bionic Beef",
          "city": "Cambridge",
          "stateProv": "Massachusetts",
          "country": "USA",
          "rookieYear": 1996,
          "robotName": "",
          "districtCode": "NE",
          "schoolName": "Cambridge Rindge & Latin HS",
          "website": "",
          "homeCMP": null
        }
        // ...more teams
      ]
    }
    ```

- **GET `/v3/:year/communityupdates/:eventCode`**

  - Returns: List of Community Updates for the teams participating in the selected event. eventCode must follow **_FIRST_** formatting. These updates are maintained by FIRST Game Announcers during the competition season, and they are designed to replace and augment the data that comes from the **_FIRST_** APIs. Typical applications will check for the existence of a value in one of these updates, and if it is not empty, replace the FIRST value with this one. There are additional properties here, such as Robot Name, Team Mottoes, and HTML formatted notes, which can be extensive.
  - **Response Object:**
    ```json
    [
      {
        "teamNumber": 133,
        "updates": {
          "nameShortLocal": "",
          "cityStateLocal": "",
          "topSponsorsLocal": "MSAD#6, Lockheed Martin, ITS, Hack Club",
          "topSponsorLocal": "",
          "sponsorsLocal": "",
          "organizationLocal": "",
          "robotNameLocal": "Bruce",
          "awardsLocal": "",
          "teamMottoLocal": "By Design",
          "teamNotesLocal": "<p>Group of clever humans</p>",
          "teamYearsNoCompeteLocal": "",
          "showRobotName": true,
          "teamNotes": "",
          "sayNumber": "",
          "awardsTextLocal": "",
          "lastUpdate": "2025-04-03T09:11:58-04:00"
        }
        ///...more updates
      }
    ]
    ```

- **GET `/v3/team/:teamNumber/updates`**
  - Returns: List of Community Updates for the teams participating in the selected event. These updates are maintained by FIRST Game Announcers during the competition season, and they are designed to replace and augment the data that comes from the **_FIRST_** APIs. Typical applications will check for the existence of a value in one of these updates, and if it is not empty, replace the FIRST value with this one. There are additional properties here, such as Robot Name, Team Mottoes, and HTML formatted notes, which can be extensive.
  - **Response Object:**
    ```json
    {
      "nameShortLocal": "",
      "cityStateLocal": "",
      "topSponsorsLocal": "Southworth, Aries & PTC",
      "topSponsorLocal": "",
      "sponsorsLocal": "",
      "organizationLocal": "Waynflete (Wayne-Fleet) School & The Baxter Academy of Technology & Science",
      "robotNameLocal": "Mattie",
      "awardsLocal": "",
      "teamMottoLocal": "",
      "teamNotesLocal": "<p>*** STRAT: Full field cycle (fast, consistent)</p><p>•&nbsp;Focused on speed (inspired by High Tide, speed added 1-2 cycle)</p><p>•&nbsp;Customized shifting swerve drive</p><p>•&nbsp;No trap mechanism</p>",
      "teamYearsNoCompeteLocal": "",
      "showRobotName": true,
      "teamNotes": "",
      "sayNumber": "fifty six eighty seven",
      "awardsTextLocal": "",
      "lastUpdate": "2025-04-03T08:53:09-04:00"
    }
    ```
- **GET `/v3/team/:teamNumber/appearances`**
  - Returns: Historical list of events at which the team appeared, sourced from TBA APIs. gatool uses this list to determine Championship and District Championship statistics for each competing team.
  - **Response Object:**
    ```json
    [
      {
        "address": "1 Crusader Way, Manchester, NH 03103, USA",
        "city": "Manchester",
        "country": "USA",
        "district": null,
        "division_keys": [],
        "end_date": "1995-04-01",
        "event_code": "cmp",
        "event_type": 4,
        "event_type_string": "Championship Finals",
        "first_event_code": "cmp",
        "first_event_id": "6633",
        "gmaps_place_id": "ChIJZbgktvRO4okRAI1dUYoad0Y",
        "gmaps_url": "https://maps.google.com/?cid=5077556286256418048",
        "key": "1995cmp",
        "lat": 42.9666738,
        "lng": -71.4344026,
        "location_name": "Manchester Memorial High School",
        "name": "National Championship",
        "parent_event_key": null,
        "playoff_type": null,
        "playoff_type_string": null,
        "postal_code": "03103",
        "short_name": "National Championship",
        "start_date": "1995-03-30",
        "state_prov": "NH",
        "timezone": "America/New_York",
        "webcasts": [],
        "website": null,
        "week": null,
        "year": 1995
      }
      ///...more appearances
    ]
    ```
- **GET `/v3/:year/team/:teamNumber/media`**
  - Returns: Images of team's robot from TBA. Used in gatool for flash cards.
  - **Response Object:**
    ```json
    [
      {
        "details": {
          "base64Image": "iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAYAAACM/rhtAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAARuSURBVFhHvZi9bxxFGIeHfKC4iQ0+kVhEAcmFIywUOCuxYnfIUjiBBcYCRBEBhhzkQ1HKKKGhcuvKTf4BN4niZiu7cWEr7t1wDRUdDZVlIQ+/37sz3pnZd/c+fOKVHu/tu3PvPJ7dmd09Y1++HhoIS7Rjg6ImB4FiM82mNe8PV1JN9guFzpw9Yxnm0XlrLgxPUk32A0VGR0dFjmFWzllzH4JvDUdSTfbK9PS0XV5edmp5mBWM4ArkHoCPcsnTiKrJOnyHY2Nj9uDgwGkxjuWvWXnTmh/RhpL3wPdgJP/OIKLxjitSBU/l7Oys3dvbE5k4EsGfyBvW3MWWo/k1uGRs42pDrR0SOYU7U1NTMir7+/s2yzK7vr4uW+7Ho6VFIMjR+9lBUW4p+iv4BXwHlsBnYN5tuY/8hXcvVgtOTk5KJ4NFIOjlUsk6KP8IvF0xghza8fFx6SSNcPg12hlbOUF2NlVuo5JKtsGHAMf6FmynxRwtnjoeF0kneG0u36mMzLbxnbSWsABwrCRIuY2NDVcgDi/48TPcKfCZbYmXFsm5tbwtc06wszYnE8O3T4nEPF+5gdIEt7e3pXAaqaAPCiw9zfNNk0tJJ4lgSaKOb2oEOVu10AU7/7/g7u6uFE4jFSyAwNMl2/QXIEI6CQWj9o5rIJQKwVJTKcg1TwsW9ddbmZZt8Xgfk6RWEOthpSAXZi28oB/BtY4VOmt3cnGO4rBOMRZtVZAdywElUkEfInC3hcJtGUWGdHIaQdxZKgXn5+elcBqpYAEEpPAlWWYwqPl+Koh9FS8V8h5A7ZIgmZiYkMJpeEGt4LOH2H75m8hJW+bRebeQf/B2UUfg08/FQo5Egk08smtRjJhOGF6wJ1JB3LObMzPVguzs1YtXrqvBIuqwXz4B/IdDp3BHEni4vPLkql6gBwaNv//9x7a+uN1d8Pr0dWvuoLMfgo55oaNpJcFMZahtFNK4/M7lSI5EO5Lglz8AfD47Eay/6MPlhCE15Im6nnBZ29zczKVTnzQhyfMowDczX8wJigjuAixM5I7glpeMz3K4j/o4aYMuZMs6mBhhPgzmVBc1ibS5AfwoJoInI8tZiLZym8vakWAeHdvGyIaCRfiFydqdnR119MRFSxJzDu+39/DSUyfIz6EgcnGUBQutIkZGRlQ5oiaJjOI44EIcCjIf4p4Sst/xuYsgtxQ8Ps5fDxirq6tSR3MgatIjAp+DLpMke57L9Sro4/Dw0DYajUo5oiZDRFI7xW5ypKc9jnpBvvzXjR5RkyGVgreAP72cwe62FUe14OLiYlc5oiZT+NTCKE0SfD13xFsaRHoV7HbdhahJDRYsCXIU3amWWYxRjKMs2I8cUZNVsPDczVuFIGGOj9fUwT8QR0dOP8UIf9fhVqtdhZqsg0JCKBlQFZTj97SadajJXhDJT92TD3+9qhDc2toaSMyjJntFJPnbH0TNY9x1vi0Ej46O7MLCwqnkiJrsFxElmDR//PVn/vmUYh41OSjDFMt5bf4DVRFNJmijL6kAAAAASUVORK5CYII="
        },
        "direct_url": "",
        "foreign_key": "avatar_2025_frc133",
        "preferred": false,
        "team_keys": ["frc133"],
        "type": "avatar",
        "view_url": ""
      }
    ]
    ```

### Events

- **GET `/v3/:year/events`**

  - Returns: All events for a given year.
  - **Response Object:**
    ```json
    {
      "Events": [
        {
          "allianceCount": "EightAlliance",
          "weekNumber": 3,
          "announcements": [],
          "code": "ALHU",
          "divisionCode": null,
          "name": "Rocket City Regional",
          "type": "Regional",
          "districtCode": null,
          "venue": "Von Braun Center",
          "city": "Huntsville",
          "stateprov": "AL",
          "country": "USA",
          "dateStart": "2025-03-12T00:00:00",
          "dateEnd": "2025-03-15T23:59:59",
          "address": "700 Monroe Street SW",
          "website": "http://firstinalabama.org/events/frc-events/",
          "webcasts": ["https://www.twitch.tv/firstinspires5"],
          "timezone": "Central Standard Time"
        }
        // ...more events
      ],
      "eventCount": 1
    }
    ```

---

### Districts

- **GET `/v3/:year/districts/`**
  - Returns: District information for the year.
  - **Response Object:**
    ```json
    {
      "districts": [
        {
          "code": "ONT",
          "name": "FIRST Ontario"
        },
        {
          "code": "PNW",
          "name": "FIRST Pacific Northwest"
        }
        // ...more Districts
      ],
      "districtCount": 12
    }
    ```

---

### Schedule & Matches

- **GET `/v3/:year/schedule/:eventCode/:tournamentLevel`**

  - Returns: Event schedule details for a tournament level (`practice`, `qual`, `playoff`). This does not include results.
  - **Response Object:**
    ```json
    {
      "Schedule": [
        {
          "description": "Qualification 1",
          "startTime": "2025-03-15T11:00:00",
          "matchNumber": 1,
          "field": "Primary",
          "tournamentLevel": "Qualification",
          "teams": [
            { "teamNumber": 4564, "station": "Red1", "surrogate": false },
            { "teamNumber": 4546, "station": "Red2", "surrogate": false },
            { "teamNumber": 7913, "station": "Red3", "surrogate": false },
            { "teamNumber": 166, "station": "Blue1", "surrogate": false },
            { "teamNumber": 501, "station": "Blue2", "surrogate": false },
            { "teamNumber": 4925, "station": "Blue3", "surrogate": false }
          ]
        }
        // ...more matches
      ]
    }
    ```

- **GET `/v3/:year/matches/:eventCode/:tournamentLevel`**

  - Returns: Match results for an event and tournament level (`qual`, `playoff`).
  - **Response Object:**

    ```json
    {
      "Matches": [
        {
          "isReplay": false,
          "matchVideoLink": "https://www.youtube.com/watch?v=9lb1uhJv9KE",
          "description": "Qualification 1",
          "matchNumber": 1,
          "scoreRedFinal": 92,
          "scoreRedFoul": 6,
          "scoreRedAuto": 9,
          "scoreBlueFinal": 75,
          "scoreBlueFoul": 18,
          "scoreBlueAuto": 12,
          "autoStartTime": "2025-03-15T10:57:25.437",
          "actualStartTime": "2025-03-15T10:57:25.437",
          "tournamentLevel": "Qualification",
          "postResultTime": "2025-03-15T11:02:26.89",
          "teams": [
            { "teamNumber": 4564, "station": "Red1", "dq": false },
            { "teamNumber": 4546, "station": "Red2", "dq": false },
            { "teamNumber": 7913, "station": "Red3", "dq": false },
            { "teamNumber": 166, "station": "Blue1", "dq": false },
            { "teamNumber": 501, "station": "Blue2", "dq": false },
            { "teamNumber": 4925, "station": "Blue3", "dq": false }
          ]
        }
        // ...more Matches
      ]
    }
    ```

- **GET `/v3/:year/schedule/hybrid/:eventCode/:tournamentLevel`**

  - Returns: Combined schedule and results for a tournament level (`qual`, `playoff`).
  - **Response Object:**

    ```json
    {
      "Schedule": {
        "schedule": [
          {
            "description": "Qualification 1",
            "startTime": "2025-03-15T11:00:00",
            "matchNumber": 1,
            "field": "Primary",
            "tournamentLevel": "Qualification",
            "teams": [
              {
                "teamNumber": 4564,
                "station": "Red1",
                "surrogate": false,
                "dq": false
              },
              {
                "teamNumber": 4546,
                "station": "Red2",
                "surrogate": false,
                "dq": false
              },
              {
                "teamNumber": 7913,
                "station": "Red3",
                "surrogate": false,
                "dq": false
              },
              {
                "teamNumber": 166,
                "station": "Blue1",
                "surrogate": false,
                "dq": false
              },
              {
                "teamNumber": 501,
                "station": "Blue2",
                "surrogate": false,
                "dq": false
              },
              {
                "teamNumber": 4925,
                "station": "Blue3",
                "surrogate": false,
                "dq": false
              }
            ],
            "isReplay": false,
            "matchVideoLink": "https://www.youtube.com/watch?v=9lb1uhJv9KE",
            "scoreRedFinal": 92,
            "scoreRedFoul": 6,
            "scoreRedAuto": 9,
            "scoreBlueFinal": 75,
            "scoreBlueFoul": 18,
            "scoreBlueAuto": 12,
            "autoStartTime": "2025-03-15T10:57:25.437",
            "actualStartTime": "2025-03-15T10:57:25.437",
            "postResultTime": "2025-03-15T11:02:26.89"
          }
          /// ...more Matches
        ],
        "headers": {
          "schedule": {
            "date": "Wed, 18 Jun 2025 20:15:56 GMT",
            "content-type": "application/json; charset=utf-8",
            "content-length": "28808",
            "connection": "keep-alive",
            "server": "Kestrel",
            "cache-control": "public, max-age=300",
            "last-modified": "Sun, 16 Mar 2025 20:06:47 GMT"
          },
          "matches": {
            "date": "Wed, 18 Jun 2025 20:16:01 GMT",
            "content-type": "application/json; charset=utf-8",
            "content-length": "42367",
            "connection": "keep-alive",
            "server": "Kestrel",
            "cache-control": "public, max-age=180",
            "last-modified": "Sun, 16 Mar 2025 20:06:47 GMT"
          }
        }
      }
    }
    ```

- **GET `/v3/:year/alliances/:eventCode`**
  - Returns: List of Alliances for the specified eventCode.
  - **Response Object:**
    ```json
    {
      "Alliances": [
        {
          "number": 1,
          "captain": 6329,
          "round1": 5687,
          "round2": 501,
          "round3": null,
          "backup": null,
          "backupReplaced": null,
          "name": "Alliance 1"
        }
        ///...more Alliances
      ],
      "count": 8
    }
    ```

---

### Scores

- **GET `/v3/:year/scores/:eventCode/:tournamentLevel/:start/:end`**

  - Returns: Full scores details for a range of matches. The score details vary per season, so you should refer to the FIRST API documentation to see the expected season specific responses. (optional: start, end).
  - **Response Object Example (2025 season):**

    ```json
    {
      "MatchScores": [
        {
          "matchLevel": "Qualification",
          "matchNumber": 1,
          "winningAlliance": 1,
          "tiebreaker": { "item1": -1, "item2": "" },
          "coopertitionBonusAchieved": false,
          "coralBonusLevelsThresholdCoop": 3,
          "coralBonusLevelsThresholdNonCoop": 4,
          "coralBonusLevelsThreshold": 4,
          "bargeBonusThreshold": 0,
          "autoBonusCoralThreshold": 1,
          "alliances": [
            {
              "alliance": "Blue",
              "autoLineRobot1": "Yes",
              "endGameRobot1": "DeepCage",
              "autoLineRobot2": "Yes",
              "endGameRobot2": "Parked",
              "autoLineRobot3": "Yes",
              "endGameRobot3": "None",
              "autoReef": {
                "topRow": {
                  "nodeA": false,
                  "nodeB": false,
                  "nodeC": false,
                  "nodeD": false,
                  "nodeE": false,
                  "nodeF": false,
                  "nodeG": false,
                  "nodeH": false,
                  "nodeI": false,
                  "nodeJ": false,
                  "nodeK": false,
                  "nodeL": false
                },
                "midRow": {
                  "nodeA": false,
                  "nodeB": false,
                  "nodeC": false,
                  "nodeD": false,
                  "nodeE": false,
                  "nodeF": false,
                  "nodeG": false,
                  "nodeH": false,
                  "nodeI": false,
                  "nodeJ": false,
                  "nodeK": false,
                  "nodeL": false
                },
                "botRow": {
                  "nodeA": false,
                  "nodeB": false,
                  "nodeC": false,
                  "nodeD": false,
                  "nodeE": false,
                  "nodeF": false,
                  "nodeG": false,
                  "nodeH": false,
                  "nodeI": false,
                  "nodeJ": false,
                  "nodeK": false,
                  "nodeL": false
                },
                "trough": 1
              },
              "autoCoralCount": 1,
              "autoMobilityPoints": 9,
              "autoPoints": 12,
              "autoCoralPoints": 3,
              "teleopReef": {
                "topRow": {
                  "nodeA": false,
                  "nodeB": false,
                  "nodeC": false,
                  "nodeD": false,
                  "nodeE": false,
                  "nodeF": false,
                  "nodeG": false,
                  "nodeH": false,
                  "nodeI": false,
                  "nodeJ": true,
                  "nodeK": true,
                  "nodeL": true
                },
                "midRow": {
                  "nodeA": false,
                  "nodeB": false,
                  "nodeC": true,
                  "nodeD": false,
                  "nodeE": false,
                  "nodeF": false,
                  "nodeG": false,
                  "nodeH": false,
                  "nodeI": false,
                  "nodeJ": false,
                  "nodeK": true,
                  "nodeL": false
                },
                "botRow": {
                  "nodeA": false,
                  "nodeB": false,
                  "nodeC": false,
                  "nodeD": false,
                  "nodeE": false,
                  "nodeF": false,
                  "nodeG": false,
                  "nodeH": false,
                  "nodeI": false,
                  "nodeJ": false,
                  "nodeK": false,
                  "nodeL": false
                },
                "trough": 0
              },
              "teleopCoralCount": 5,
              "teleopPoints": 45,
              "teleopCoralPoints": 23,
              "algaePoints": 8,
              "netAlgaeCount": 2,
              "wallAlgaeCount": 0,
              "endGameBargePoints": 14,
              "autoBonusAchieved": true,
              "coralBonusAchieved": false,
              "bargeBonusAchieved": true,
              "coopertitionCriteriaMet": false,
              "foulCount": 0,
              "techFoulCount": 1,
              "g206Penalty": false,
              "g410Penalty": false,
              "g418Penalty": true,
              "g428Penalty": false,
              "adjustPoints": 0,
              "foulPoints": 18,
              "rp": 2,
              "totalPoints": 75
            },
            {
              "alliance": "Red",
              "autoLineRobot1": "Yes",
              "endGameRobot1": "None",
              "autoLineRobot2": "Yes",
              "endGameRobot2": "None",
              "autoLineRobot3": "Yes",
              "endGameRobot3": "None",
              "autoReef": {
                "topRow": {
                  "nodeA": false,
                  "nodeB": false,
                  "nodeC": false,
                  "nodeD": false,
                  "nodeE": false,
                  "nodeF": false,
                  "nodeG": false,
                  "nodeH": false,
                  "nodeI": false,
                  "nodeJ": false,
                  "nodeK": false,
                  "nodeL": false
                },
                "midRow": {
                  "nodeA": false,
                  "nodeB": false,
                  "nodeC": false,
                  "nodeD": false,
                  "nodeE": false,
                  "nodeF": false,
                  "nodeG": false,
                  "nodeH": false,
                  "nodeI": false,
                  "nodeJ": false,
                  "nodeK": false,
                  "nodeL": false
                },
                "botRow": {
                  "nodeA": false,
                  "nodeB": false,
                  "nodeC": false,
                  "nodeD": false,
                  "nodeE": false,
                  "nodeF": false,
                  "nodeG": false,
                  "nodeH": false,
                  "nodeI": false,
                  "nodeJ": false,
                  "nodeK": false,
                  "nodeL": false
                },
                "trough": 0
              },
              "autoCoralCount": 0,
              "autoMobilityPoints": 9,
              "autoPoints": 9,
              "autoCoralPoints": 0,
              "teleopReef": {
                "topRow": {
                  "nodeA": true,
                  "nodeB": true,
                  "nodeC": true,
                  "nodeD": true,
                  "nodeE": false,
                  "nodeF": false,
                  "nodeG": false,
                  "nodeH": true,
                  "nodeI": false,
                  "nodeJ": false,
                  "nodeK": true,
                  "nodeL": false
                },
                "midRow": {
                  "nodeA": false,
                  "nodeB": false,
                  "nodeC": false,
                  "nodeD": false,
                  "nodeE": false,
                  "nodeF": false,
                  "nodeG": true,
                  "nodeH": false,
                  "nodeI": true,
                  "nodeJ": true,
                  "nodeK": true,
                  "nodeL": true
                },
                "botRow": {
                  "nodeA": true,
                  "nodeB": true,
                  "nodeC": false,
                  "nodeD": false,
                  "nodeE": false,
                  "nodeF": false,
                  "nodeG": false,
                  "nodeH": false,
                  "nodeI": false,
                  "nodeJ": true,
                  "nodeK": true,
                  "nodeL": true
                },
                "trough": 0
              },
              "teleopCoralCount": 16,
              "teleopPoints": 77,
              "teleopCoralPoints": 65,
              "algaePoints": 12,
              "netAlgaeCount": 0,
              "wallAlgaeCount": 2,
              "endGameBargePoints": 0,
              "autoBonusAchieved": false,
              "coralBonusAchieved": false,
              "bargeBonusAchieved": true,
              "coopertitionCriteriaMet": true,
              "foulCount": 0,
              "techFoulCount": 3,
              "g206Penalty": false,
              "g410Penalty": false,
              "g418Penalty": false,
              "g428Penalty": false,
              "adjustPoints": 0,
              "foulPoints": 6,
              "rp": 4,
              "totalPoints": 92
            }
            ///...more scores
          ]
        }
      ]
    }
    ```

- **GET `/v3/:year/highscores/:eventCode`**

  - Returns: High scores for a specified eventCode. Returns 8 high score values, based on the following criteria:
    - No penalties (clean play), Qual and Playoff (type: `penaltyFree`)
    - No penalty points awarded to winner, Qual and Playoff (type: `TBAPenaltyFree`)
    - Offsetting penalties (no advantage to either Alliance), Qual and Playoff (type: `offsetting`)
    - Overall high scoring match, Qual and Playoff (type: `overall`)
  - **Response Object Example:**

    ```json
    [
      {
        "yearType": "2025overallplayoff",
        "year": 2025,
        "type": "overall",
        "level": "playoff",
        "matchData": {
          "event": {
            "eventCode": "MELEW",
            "districtCode": "NE",
            "type": "playoff"
          },
          "highScoreAlliance": "red",
          "match": {
            "description": "Match 11 (R4)",
            "startTime": "2025-03-16T14:33:00",
            "matchNumber": 11,
            "field": "Primary",
            "tournamentLevel": "Playoff",
            "teams": [
              {
                "teamNumber": 6329,
                "station": "Red1",
                "surrogate": false,
                "dq": false
              },
              {
                "teamNumber": 501,
                "station": "Red2",
                "surrogate": false,
                "dq": false
              },
              {
                "teamNumber": 5687,
                "station": "Red3",
                "surrogate": false,
                "dq": false
              },
              {
                "teamNumber": 8023,
                "station": "Blue1",
                "surrogate": false,
                "dq": false
              },
              {
                "teamNumber": 5813,
                "station": "Blue2",
                "surrogate": false,
                "dq": false
              },
              {
                "teamNumber": 4909,
                "station": "Blue3",
                "surrogate": false,
                "dq": false
              }
            ],
            "isReplay": false,
            "matchVideoLink": "https://www.youtube.com/watch?v=GlkAhGepncE",
            "scoreRedFinal": 199,
            "scoreRedFoul": 0,
            "scoreRedAuto": 51,
            "scoreBlueFinal": 149,
            "scoreBlueFoul": 8,
            "scoreBlueAuto": 44,
            "autoStartTime": "2025-03-16T14:45:46.577",
            "actualStartTime": "2025-03-16T14:45:46.577",
            "postResultTime": "2025-03-16T14:49:36.19"
          }
        }
        ///...more high scores
      }
    ]
    ```

- **GET `/v3/:year/highscores`**

  - Returns: High scores for a specified year. Returns 8 high score values for each District and 8 high score values for the entire season, based on the following criteria:
    - No penalties (clean play), Qual and Playoff (type: `penaltyFree`)
    - No penalty points awarded to winner, Qual and Playoff (type: `TBAPenaltyFree`)
    - Offsetting penalties (no advantage to either Alliance), Qual and Playoff (type: `offsetting`)
    - Overall high scoring match, Qual and Playoff (type: `overall`)
  - **Response Object Example:**

    ```json
    [
      {
        "yearType": "2025DistrictCHSOverallplayoff",
        "year": 2025,
        "type": "DistrictCHSOverall",
        "level": "playoff",
        "matchData": {
          "event": {
            "eventCode": "CHCMP",
            "districtCode": "CHS",
            "type": "playoff"
          },
          "highScoreAlliance": "red",
          "match": {
            "description": "Match 6 (R2)",
            "startTime": "2025-04-06T13:45:00",
            "matchNumber": 6,
            "field": "Primary",
            "tournamentLevel": "Playoff",
            "teams": [
              {
                "teamNumber": 1731,
                "station": "Red1",
                "surrogate": false,
                "dq": false
              },
              {
                "teamNumber": 686,
                "station": "Red2",
                "surrogate": false,
                "dq": false
              },
              {
                "teamNumber": 620,
                "station": "Red3",
                "surrogate": false,
                "dq": false
              },
              {
                "teamNumber": 122,
                "station": "Blue1",
                "surrogate": false,
                "dq": false
              },
              {
                "teamNumber": 1885,
                "station": "Blue2",
                "surrogate": false,
                "dq": false
              },
              {
                "teamNumber": 3136,
                "station": "Blue3",
                "surrogate": false,
                "dq": false
              }
            ],
            "isReplay": false,
            "matchVideoLink": null,
            "scoreRedFinal": 246,
            "scoreRedFoul": 0,
            "scoreRedAuto": 55,
            "scoreBlueFinal": 220,
            "scoreBlueFoul": 0,
            "scoreBlueAuto": 37,
            "autoStartTime": "2025-04-06T13:49:44.62",
            "actualStartTime": "2025-04-06T13:49:44.62",
            "postResultTime": "2025-04-06T13:55:01.98"
          }
        }
      }
      ///...more high scores
    ]
    ```

---

### Team Avatars

- **GET `/v3/:year/avatars/team/:teamNumber/avatar.png`**
  - Returns: Binary PNG avatar for a registered team in the requested year.
  - **Response Object:** Binary image (not JSON)\
    When team does not exist for a given season, or when there is no avatar defined for the team, the following JSON is returned:
  ```json
  { "message": "Malformed Parameter Format In Request : Team number 6 was not found" }
  ```

---

### District Rankings

- **GET `/v3/:year/district/rankings/:districtCode`**
  - Returns: District ranking information for a specified District Code (**_FIRST_** format).
  - **Response Object:**
    ```json
    {
      "districtRanks": [
        {
          "districtCode": "NE",
          "teamNumber": 190,
          "rank": 1,
          "totalPoints": 349,
          "event1Code": "MEFAL",
          "event1Points": 72,
          "event2Code": "RIKIN",
          "event2Points": 73,
          "districtCmpCode": "NECMP1",
          "districtCmpPoints": 204,
          "teamAgePoints": 0,
          "adjustmentPoints": 0,
          "qualifiedDistrictCmp": true,
          "qualifiedFirstCmp": true
        }
        /// ...more Ranks
      ],
      "rankingCountTotal": 191,
      "rankingCountPage": 191,
      "pageCurrent": 1,
      "pageTotal": 1
    }
    ```

---

### Awards

- **GET `/v3/:year/awards/event/:eventCode`**

  - Returns: Awards for a specific eventCode (**_FIRST_** format).
  - **Response Object:**

    ```json
    {
      "Awards": [
        {
          "awardId": 703,
          "teamId": 1557010,
          "eventId": 68746,
          "eventDivisionId": null,
          "eventCode": "NHDUR",
          "name": "District FIRST Impact Award",
          "series": 1,
          "teamNumber": 172,
          "schoolName": "Falmouth High School & Gorham High School",
          "fullTeamName": "Maine APP Challenge, Tyler Technologies/Brookfield Properties/Adobe/GoFar/Falmouth High School/Texas Instruments/IDEXX Laboratories&Falmouth High School&Gorham High School",
          "person": null,
          "cmpQualifying": false,
          "cmpQualifyingReason": null
        }
        ///...more awards
      ]
    }
    ```

- **GET `/v3/:year/awards/team/:teamNumber`**
  - Returns: Award data for a team in a given year.
  - **Response Object:**
    ```json
    {
      "Awards": [
        {
          "Awards": [
            {
              "awardId": 611,
              "teamId": 1644703,
              "eventId": 73793,
              "eventDivisionId": null,
              "eventCode": "MEFAL",
              "name": "District Event Finalist",
              "series": 2,
              "teamNumber": 58,
              "schoolName": "South Portland High School",
              "fullTeamName": "South Portland School Department/Casco Point Psychological Services/Generac/Building STEAM/Diodes Incorporated/IBEW Local 567/The Hews Company&South Portland High School",
              "person": null,
              "cmpQualifying": false,
              "cmpQualifyingReason": null
            }
            ///...more awards if available
          ]
        }
      ]
    }
    ```

---

## Notes

- All endpoints return JSON unless otherwise noted.
- Types are inferred from code and may change based on data sources (FIRST, TBA).
- Some endpoints return arrays, others wrap results in an object with a property like "teams", "Events", "districts", etc.

---
