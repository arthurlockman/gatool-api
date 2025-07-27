/* eslint-disable @typescript-eslint/no-unused-vars */

interface FTCTeam {
  teamNumber: number;
  station: Station;
  dq: boolean;
  onField: boolean;
}

interface FTCMatch {
  actualStartTime: string;
  description: string;
  tournamentLevel: string;
  series: number;
  postResultTime: string;
  matchNumber: number;
  scoreRedFinal: number;
  scoreRedFoul: number;
  scoreRedAuto: number;
  scoreBlueFinal: number;
  scoreBlueFoul: number;
  scoreBlueAuto: number;
  teams: FTCTeam[];
}

interface FTCMatchResponse {
  matches: FTCMatch[];
  Matches?: FTCMatch[];
}
