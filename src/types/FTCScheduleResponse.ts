/* eslint-disable @typescript-eslint/no-unused-vars */
type FTCField =  "1" | '2' | '3' | '4' | '5' | '6' ; // Add other fields as needed

interface FTCScheduleTeam {
  teamNumber: number;
  displayTeamNumber: string;
  station: Station;
  team: string;
  teamName: string;
  surrogate: boolean;
  noShow: boolean;
}

interface FTCScheduleMatch {
  description: string;
  field: FTCField;
  tournamentLevel: string;
  startTime: string;
  series: number;
  matchNumber: number;
  teams: FTCScheduleTeam[];
}

interface FTCScheduleResponse {
  schedule: FTCScheduleMatch[];
  Schedule?: FTCScheduleMatch[];
}

interface FTCHybridScheduleResponse {
  schedule: FTCHybridMatch[];
  Schedule?: FTCHybridMatch[];
}

interface FTCHybridMatch {
  description: string;
  tournamentLevel: string;
  series: number;
  matchNumber: number;
  startTime: string;
  actualStartTime: string;
  postResultTime: string;
  scoreRedFinal: number;
  scoreRedFoul: number;
  scoreRedAuto: number;
  scoreBlueFinal: number;
  scoreBlueFoul: number;
  scoreBlueAuto: number;
  scoreBlueDriveControlled: number | null;
  scoreBlueEndgame: number | null;
  scoreRedDriveControlled: number | null;
  scoreRedEndgame: number | null;
  redWins: boolean;
  blueWins: boolean;
  teams: FTCHybridTeam[];
}

interface FTCHybridTeam {
  teamNumber: number;
  displayTeamNumber: string;
  station: Station;
  surrogate: boolean;
  noShow: boolean;
  dq: boolean;
  onField: boolean;
  teamName: string;
}
