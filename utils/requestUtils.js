import {GetFRCApiToken, GetTBAApiToken} from './secretUtils.js'
import * as fs from 'fs';

var {got} = await import('got')

var mozillaCA = fs.readFileSync('node_modules/node_extra_ca_certs_mozilla_bundle/ca_bundle/ca_intermediate_root_bundle.pem')

/**
 * Get data from TBA and return a promise
 * @param path The path to GET data from
 */
const GetDataFromTBA = async (path) => {
    const data = await got.get(`https://www.thebluealliance.com/api/v3/${path}`, {
        headers: {
            'X-TBA-Auth-Key': await GetTBAApiToken(),
            'Accept': 'application/json'
        },
        timeout: {
            request: 20000
        }
    })
    return {
        body: JSON.parse(data.body),
        headers: data.headers
    }
}

/**
 * Get data from FIRST and return a promise
 * @param path The path to GET data from
 */
const GetDataFromFIRST = async (path, apiVersion = 'v3.0') => {
    const data = await got.get(`https://frc-api.firstinspires.org/${apiVersion}/${path}`, {
        headers: {
            'Authorization': await GetFRCApiToken(),
            'Accept': 'application/json'
        },
        https: {
            certificateAuthority: mozillaCA
        }
    })
    return {
        body: JSON.parse(data.body),
        headers: data.headers
    }
}

export {GetDataFromFIRST, GetDataFromTBA}