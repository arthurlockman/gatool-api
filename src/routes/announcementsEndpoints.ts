import express from 'express';
import { GetAnnouncements, GetEventAnnouncements } from '../utils/storageUtils';
import logger from '../logger';

export const router = express.Router();

router.get('/', async (_, res) => {
  res.setHeader('Cache-Control', 'no-cache');
  try {
    const prefs = await GetAnnouncements();
    res.json(JSON.parse(prefs));
  } catch (e) {
    logger.error(e);
    res.status(404).send();
  }
});

router.get('/:eventCode', async (req, res) => {
  res.setHeader('Cache-Control', 'no-cache');
  try {
    const prefs = await GetEventAnnouncements(req.params.eventCode);
    res.json(JSON.parse(prefs));
  } catch (e) {
    logger.error(e);
    res.status(404).send();
  }
});