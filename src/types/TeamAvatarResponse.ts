/* eslint-disable @typescript-eslint/no-unused-vars */
interface TeamAvatarResponse {
  teams: TeamAvatar[];
  teamCountTotal: number;
  teamCountPage: number;
  pageCurrent: number;
  pageTotal: number;
}

interface TeamAvatar {
  teamNumber: number;
  encodedAvatar: string;
}
