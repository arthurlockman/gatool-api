import { GetFRCApiToken, GetTBAApiToken } from './secretUtils.js'

var { got } = await import('got')

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
        }
    })
    data.body.headers = data.headers;
    return {
        body: JSON.parse(data.body),
        headers: data.headers
    }
}

export { GetDataFromFIRST, GetDataFromTBA }