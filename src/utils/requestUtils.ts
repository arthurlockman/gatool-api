import { GetFRCApiToken, GetTBAApiToken, GetTOAApiToken, GetFTCApiToken } from './secretUtils';
import * as fs from 'fs';
import logger from '../logger';
import { RequestError } from 'got';

const { got } = await import('got');

const mozillaCA = fs.readFileSync(
  'node_modules/node_extra_ca_certs_mozilla_bundle/ca_bundle/ca_intermediate_root_bundle.pem'
);

/**
 * Get data from TBA and return a promise
 * @param path The path to GET data from
 */
const GetDataFromTBA = async <T>(path: string) => {
  try {
    const data = await got.get(`https://www.thebluealliance.com/api/v3/${path}`, {
      headers: {
        'X-TBA-Auth-Key': await GetTBAApiToken(),
        Accept: 'application/json'
      },
      timeout: {
        request: 20000
      }
    });
    return {
      body: JSON.parse(data.body) as T,
      headers: data.headers
    };
  } catch (error) {
    if (error instanceof RequestError) {
      logger.error(`Error code ${error.code} from TBA: ${error.message}`, error);
    } else {
      logger.error(error);
    }
    throw error;
  }
};

/**
 * Get data from TOA and return a promise
 * @param path The path to GET data from
 */
const GetDataFromTOA = async <T>(path: string) => {
  try {
    const data = await got.get(`https://theorangealliance.org/api/${path}`, {
      headers: {
        'X-TOA-Key': await GetTOAApiToken(),
        'X-Application-Origin': await GetTOAApiToken(),
        Accept: 'application/json'
      },
      timeout: {
        request: 20000
      }
    });
    return {
      body: JSON.parse(data.body) as T,
      headers: data.headers
    };
  } catch (error) {
    if (error instanceof RequestError) {
      logger.error(`Error code ${error.code} from TBA: ${error.message}`, error);
    } else {
      logger.error(error);
    }
    throw error;
  }
};

/**
 * Get data from FIRST and return a promise
 * @param path The path to GET data from
 * @param apiVersion version of the FRC API
 */
const GetDataFromFIRST = async <T>(path: string, apiVersion = 'v3.0') => {
  try {
    const data = await got.get(`https://frc-api.firstinspires.org/${apiVersion}/${path}`, {
      headers: {
        Authorization: await GetFRCApiToken(),
        Accept: 'application/json'
      },
      https: {
        certificateAuthority: mozillaCA
      }
    });
    return {
      body: JSON.parse(data.body) as T,
      headers: data.headers
    };
  } catch (error) {
    if (error instanceof RequestError) {
      logger.error(`Error code ${error.code} from FIRST: ${error.message}`, error);
    } else {
      logger.error(error);
    }
    throw error;
  }
};

/**
 * Get data from FIRST and return a promise
 * @param path The path to GET data from
 * @param apiVersion version of the FRC API
 */
const GetDataFromFTC = async <T>(path: string, apiVersion = 'v2.0') => {
  try {
    const data = await got.get(`https://ftc-api.firstinspires.org/${apiVersion}/${path}`, {
      headers: {
        Authorization: await GetFTCApiToken(),
        Accept: 'application/json'
      },
      https: {
        certificateAuthority: mozillaCA
      }
    });
    return {
      body: JSON.parse(data.body) as T,
      headers: data.headers
    };
  } catch (error) {
    if (error instanceof RequestError) {
      logger.error(`Error code ${error.code} from FIRST: ${error.message}`, error);
    } else {
      logger.error(error);
    }
    throw error;
  }
};

export { GetDataFromFIRST, GetDataFromTBA, GetDataFromTOA, GetDataFromFTC };
