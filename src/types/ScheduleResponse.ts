type Field = 'Primary'; // Add other fields as needed

interface ScheduleTeam {
  teamNumber: number;
  station: Station;
  surrogate: boolean;
}

interface ScheduleMatch {
  field: Field;
  tournamentLevel: string;
  description: string;
  startTime: string;
  matchNumber: number;
  teams: ScheduleTeam[];
}

interface ScheduleResponse {
  Schedule: ScheduleMatch[];
  schedule?: ScheduleMatch[];
}

interface HybridMatch {
  field: Field;
  startTime: string;
  matchNumber: number;
  actualStartTime: string;
  tournamentLevel: string;
  postResultTime: string;
  description: string;
  scoreRedFinal: number;
  scoreRedFoul: number;
  scoreRedAuto: number;
  scoreBlueFinal: number;
  scoreBlueFoul: number;
  scoreBlueAuto: number;
  teams: HybridTeam[];
}

interface HybridTeam {
  teamNumber: number;
  station: Station;
  surrogate: boolean;
  dq: boolean;
}