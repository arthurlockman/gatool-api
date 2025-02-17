import { GetSubscribedUsers } from './mailChimpUtils';
import logger from '../logger';
import { AssignUserRoles, CreateUser, DeleteUser, GetUser, RemoveUserRoles } from './auth0Utils';
import { StoreUserSyncResults } from './storageUtils';
import { GetUsers200ResponseOneOfInner } from 'auth0';

const optInText = 'I want access to gatool and agree that I will not abuse this access to team data.';

const delay = async (ms: number) => {
  await new Promise((resolve) => setTimeout(resolve, ms));
};

export const SyncUsers = async () => {
  const listMembers = await GetSubscribedUsers();
  const optedInUsers = listMembers
    .filter((member) => member.status === 'subscribed' && member.merge_fields.GATOOL === optInText)
    .map((member) => member.email_address.toLocaleLowerCase());
  logger.info(`There are ${optedInUsers.length} users opted in. Assigning user role.`);
  for (const email of optedInUsers) {
    // auth0 has an annoying rate limit on the management API so we have to slow the process down
    await delay(800);
    const user = await GetOrCreateUser(email);
    if (user == null) continue;
    logger.info(`Assigning 'user' role to ${email}...`);
    await AssignUserRoles(user.user_id, ['rol_KRLODHx3eNItUgvI']);
  }
  const optedOutUsers = listMembers
    .filter((member) => member.status === 'subscribed' && member.merge_fields.GATOOL !== optInText)
    .map((member) => member.email_address.toLocaleLowerCase());
  logger.info(`There are ${optedOutUsers.length} users opted out. Assigning viewer role and removing user role.`);
  for (const email of optedOutUsers) {
    await delay(800);
    const user = await GetOrCreateUser(email);
    if (user == null) continue;
    await RemoveUserRoles(user.user_id, ['rol_KRLODHx3eNItUgvI']);
    await AssignUserRoles(user.user_id, ['rol_EQcREtmOWaGanRYG']);
    logger.info(`Removed editing permissions from user ${email}.`);
  }
  const unsubscribedUsers = listMembers
    .filter((member) => member.status === 'unsubscribed')
    .map((member) => member.email_address.toLocaleLowerCase());
  logger.info(`There are ${unsubscribedUsers.length} users unsubscribed. Deleting accounts.`);
  let deletedUsers = 0;
  for (const email of unsubscribedUsers) {
    const user = await GetUser(email);
    if (user == null) {
      logger.info(`User ${email} does not exist. No need to delete.`);
    } else {
      await DeleteUser(user.user_id);
      logger.info(`Deleted user ${email}.`);
      deletedUsers++;
    }
    await delay(800);
  }
  const syncDate = new Date().toISOString();
  await StoreUserSyncResults(syncDate, optedInUsers.length, optedOutUsers.length, deletedUsers);
};

const GetOrCreateUser = async (email: string): Promise<GetUsers200ResponseOneOfInner | null> => {
  let user = await GetUser(email);
  if (user == null) {
    logger.info(`User ${email} does not exist, creating...`);
    await CreateUser(email);
    user = await GetUser(email);
    if (user == null) {
      logger.info(`Couldn't create user ${email}. Moving on, will retry next time.`);
    }
  }
  return user;
};
