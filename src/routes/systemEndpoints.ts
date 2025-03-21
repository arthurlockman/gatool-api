import express from 'express';
import { GetAnnouncements, GetEventAnnouncements, GetUserSyncResults, StoreAnnouncements, StoreEventAnnouncements } from '../utils/storageUtils';
import { AssignUserRoles, CreateUser, GetUser } from '../utils/auth0Utils';
import { SyncUsers } from '../utils/syncUsers';
import logger from '../logger';

export const router = express.Router();

// Announcement storage and retrieval
router.get('/announcements', async (_, res) => {
  res.setHeader('Cache-Control', 'no-cache');
  try {
    const prefs = await GetAnnouncements();
    res.json(JSON.parse(prefs));
  } catch (e) {
    logger.error(e);
    res.status(404).send();
  }
});

router.get('/announcements/:eventCode', async (req, res) => {
  res.setHeader('Cache-Control', 'no-cache');
  try {
    const prefs = await GetEventAnnouncements(req.params.eventCode);
    res.json(JSON.parse(prefs));
  } catch (e) {
    logger.error(e);
    res.status(404).send();
  }
});

router.put('/announcements', async (req, res) => {
  ensureAdmin(req, res);
  await StoreAnnouncements(req.body);
  res.status(204).send();
});

router.put('/announcements/:eventCode', async (req, res) => {
  ensureUser(req, res);
  await StoreEventAnnouncements(req.body,req.params.eventCode);
  res.status(204).send();
});

router.get('/admin/users/:email', async (req, res) => {
  ensureAdmin(req, res);
  const user = await GetUser(req.params.email);
  res.json(user);
});

router.post('/admin/users', async (req, res) => {
  ensureAdmin(req, res);
  await CreateUser(req.body.email);
  res.status(204).send();
});

router.post('/admin/users/:email/roles', async (req, res) => {
  ensureAdmin(req, res);
  const roles = req.body.roles;
  const user = await GetUser(req.params.email);
  if (user?.user_id) {
    await AssignUserRoles(user.user_id, roles);
    res.status(204).send();
  }
  res.status(500).send();
});

router.post('/admin/syncusers', async (req, res) => {
  ensureAdmin(req, res);
  await SyncUsers();
  res.status(204).send();
});

router.get('/admin/syncusers', async (req, res) => {
  ensureAdmin(req, res);
  try {
    const results = await GetUserSyncResults();
    res.json(JSON.parse(results));
  } catch (e) {
    logger.error(e);
    res.status(404).send();
  }
});

const ensureAdmin = (req: express.Request, res: express.Response) => {
  if (!(req.auth?.payload['https://gatool.org/roles'] as string[]).includes('admin')) {
    res.status(403).send();
  }
};

const ensureUser = (req: express.Request, res: express.Response) => {
  if (!(req.auth?.payload['https://gatool.org/roles'] as string[]).includes('user')) {
    res.status(403).send();
  }
};
