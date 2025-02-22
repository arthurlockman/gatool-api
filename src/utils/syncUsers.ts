import { CloneAndSendWelcomeCampaign, GetSubscribedUsers } from './mailChimpUtils';
import logger from '../logger';
import { AssignUserRoles, CreateUser, DeleteUser, GetUser, RemoveUserRoles } from './auth0Utils';
import { StoreUserSyncResults } from './storageUtils';
import { GetUsers200ResponseOneOfInner } from 'auth0';

const optInText = 'I want access to gatool and agree that I will not abuse this access to team data.';

const delay = async (ms: number) => {
  await new Promise((resolve) => setTimeout(resolve, ms));
};

const fullUserRole = 'rol_KRLODHx3eNItUgvI';
const readOnlyRole = 'rol_EQcREtmOWaGanRYG';

export const SyncUsers = async () => {
  let addedUsers = false;
  const listMembers = await GetSubscribedUsers();
  const optedInUsers = listMembers
    .filter((member) => member.status === 'subscribed' && member.merge_fields.GATOOL === optInText)
    .map((member) => member.email_address.toLocaleLowerCase());
  logger.info(`There are ${optedInUsers.length} users opted in. Assigning user role.`);
  for (const email of optedInUsers) {
    // auth0 has an annoying rate limit on the management API so we have to slow the process down
    await delay(800);
    const [user, created] = await GetOrCreateUser(email);
    if (user == null) continue;
    addedUsers = addedUsers || created;
    logger.info(`Assigning 'user' role to ${email}...`);
    await AssignUserRoles(user.user_id, [fullUserRole]);
  }

  const optedOutUsers = listMembers
    .filter((member) => member.status === 'subscribed' && member.merge_fields.GATOOL !== optInText)
    .map((member) => member.email_address.toLocaleLowerCase());
  logger.info(`There are ${optedOutUsers.length} users opted out. Assigning viewer role and removing user role.`);
  for (const email of optedOutUsers) {
    await delay(800);
    const [user, created] = await GetOrCreateUser(email);
    if (user == null) continue;
    addedUsers = addedUsers || created;
    await RemoveUserRoles(user.user_id, [fullUserRole]);
    await AssignUserRoles(user.user_id, [readOnlyRole]);
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
  if (addedUsers) {
    logger.info('Added users, sending new welcome campaign...');
    await CloneAndSendWelcomeCampaign();
  } else {
    logger.info('Did not add any new users, no welcome campaign sent.')
  }
  logger.info('Sync complete.');
};

const GetOrCreateUser = async (email: string): Promise<[GetUsers200ResponseOneOfInner | null, boolean]> => {
  let user = await GetUser(email);
  let created = false;
  if (user == null) {
    logger.info(`User ${email} does not exist, creating...`);
    await CreateUser(email);
    user = await GetUser(email);
    if (user == null) {
      logger.info(`Couldn't create user ${email}. Moving on, will retry next time.`);
    }
    created = true;
  }
  return [user, created];
};
