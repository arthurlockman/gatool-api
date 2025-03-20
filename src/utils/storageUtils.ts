import { ReadSecret } from './secretUtils';
import { BlobServiceClient } from '@azure/storage-blob';

const blobStorageConnectionString = await ReadSecret('UserStorageConnectionString');
const blobServiceClient = BlobServiceClient.fromConnectionString(blobStorageConnectionString);

const userPrefsContainer = blobServiceClient.getContainerClient('gatool-user-preferences');
const teamUpdatesContainer = blobServiceClient.getContainerClient('gatool-team-updates');
const teamUpdateHistoryContainer = blobServiceClient.getContainerClient('gatool-team-updates-history');
const highScoresContainer = blobServiceClient.getContainerClient('gatool-high-scores');

/**
 * Get stored user preferences.
 * @param userName The username to retrieve.
 */
export const GetUserPreferences = async (userName: string) => {
  const userBlob = userPrefsContainer.getBlockBlobClient(`${userName}.prefs.json`);
  const content = await userBlob.download(0);
  return await streamToString(content.readableStreamBody);
};

/**
 * Store a user's preferences.
 * @param userName The username
 * @param preferences The preferences to store.
 */
export const StoreUserPreferences = async (userName: string, preferences: any) => {
  const userBlob = userPrefsContainer.getBlockBlobClient(`${userName}.prefs.json`);
  const data = JSON.stringify(preferences);
  await userBlob.upload(data, data.length);
};

/**
 * Get stored system announcements
 */
export const GetAnnouncements = async () => {
  const userBlob = userPrefsContainer.getBlockBlobClient(`system.announce.json`);
  if(await userBlob.exists()) {
    const content = await userBlob.download(0);
    return await streamToString(content.readableStreamBody);
  } else {
    return 'null';
  }
};

/**
 * Store system announcements
 * @param announcements The announcements to store.
 */
export const StoreAnnouncements = async (announcements: any) => {
  const userBlob = userPrefsContainer.getBlockBlobClient(`system.announce.json`);
  const data = JSON.stringify(announcements);
  await userBlob.upload(data, data.length);
};

export const StoreUserSyncResults = async (
  timestamp: string,
  fullUsers: number,
  readOnlyUsers: number,
  deletedUsers: number
) => {
  const blob = userPrefsContainer.getBlockBlobClient(`system.userSync.json`);
  const data = JSON.stringify({
    timestamp,
    fullUsers,
    readOnlyUsers,
    deletedUsers
  });
  await blob.upload(data, data.length);
};

export const GetUserSyncResults = async () => {
  const blob = userPrefsContainer.getBlockBlobClient(`system.userSync.json`);
  const content = await blob.download(0);
  return await streamToString(content.readableStreamBody);
};

/**
 * Get all stored team updates for a team
 * @param teamNumber The team number to get updates for
 */
export const GetTeamUpdates = async (teamNumber: number) => {
  const userBlob = teamUpdatesContainer.getBlockBlobClient(`${teamNumber}.json`);
  const content = await userBlob.download(0);
  return await streamToString(content.readableStreamBody);
};

/**
 * Get all historical update versions for a team
 * @param teamNumber The team number to get update history for
 */
export const GetTeamUpdateHistory = async (teamNumber: number) => {
  const iterator = teamUpdateHistoryContainer
    .listBlobsFlat({
      prefix: `${teamNumber}/`
    })
    .byPage({ maxPageSize: 1000 });
  const response = (await iterator.next()).value;
  let r: any[] = [];
  for (const blob of response.segment.blobItems) {
    const b = teamUpdateHistoryContainer.getBlockBlobClient(blob.name);
    const c = await b.download(0);
    const u = JSON.parse(await streamToString(c.readableStreamBody));
    u.modifiedDate = blob.name.replace(`${teamNumber}/`, '').replace(`.json`, '');
    r = r.concat(u);
  }
  return r;
};

/**
 * Store a team update blob
 * @param teamNumber the team number
 * @param data the update data to store
 * @param email the email of the user making the update
 */
export const StoreTeamUpdates = async (teamNumber: number, data: any, email: string) => {
  try {
    const blob = teamUpdatesContainer.getBlockBlobClient(`${teamNumber}.json`);
    const lastModifiedDate = (await blob.getProperties()).lastModified as Date;
    const b = await blob.download(0);
    const content = await streamToString(b.readableStreamBody);
    const historyBlob = teamUpdateHistoryContainer.getBlockBlobClient(
      `${teamNumber}/${lastModifiedDate.toJSON()}.json`
    );
    await historyBlob.upload(content, content.length);
  } catch {
    // No stored updates, continue without saving history
  }
  const userBlob = teamUpdatesContainer.getBlockBlobClient(`${teamNumber}.json`);
  data.source = email;
  const d = JSON.stringify(data);
  await userBlob.upload(d, d.length);
};

export const StoreHighScores = async (year: number, type: string, level: string, match: any) => {
  const scoreBlob = highScoresContainer.getBlockBlobClient(`${year}-${type}-${level}.json`);
  const item = {
    yearType: year + type + level,
    year,
    type,
    level,
    matchData: match
  };
  const d = JSON.stringify(item);
  await scoreBlob.upload(d, d.length);
};

export const GetHighScores = async (year: string) => {
  const iterator = highScoresContainer
    .listBlobsFlat({
      prefix: year
    })
    .byPage({ maxPageSize: 1000 });
  const response = (await iterator.next()).value;
  let r: any[] = [];
  for (const blob of response.segment.blobItems) {
    const b = highScoresContainer.getBlockBlobClient(blob.name);
    const c = await b.download(0);
    r = r.concat(JSON.parse(await streamToString(c.readableStreamBody)));
  }
  return r;
};

const streamToString = (stream: NodeJS.ReadableStream | undefined): Promise<string> => {
  const chunks: any[] = [];
  const s = stream as NodeJS.ReadableStream;
  return new Promise((resolve, reject) => {
    s.on('data', (chunk) => chunks.push(Buffer.from(chunk)));
    s.on('error', (err) => reject(err));
    s.on('end', () => resolve(Buffer.concat(chunks).toString('utf8')));
  });
};
