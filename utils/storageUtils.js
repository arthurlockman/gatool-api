import { ReadSecret } from "./secretUtils.js"
import { BlobServiceClient } from '@azure/storage-blob'

const blobStorageConnectionString = await ReadSecret('UserStorageConnectionString')
const blobServiceClient = BlobServiceClient.fromConnectionString(
    blobStorageConnectionString
)

const userPrefsContainer = blobServiceClient.getContainerClient('gatool-user-preferences')
const teamUpdatesContainer = blobServiceClient.getContainerClient('gatool-team-updates')
const highScoresContainer = blobServiceClient.getContainerClient('gatool-high-scores')

/**
 * Get stored user preferences.
 * @param userName The username to retrieve.
 */
export const GetUserPreferences = async (userName) => {
    var userBlob = userPrefsContainer.getBlockBlobClient(`${userName}.prefs.json`)
    var content = await userBlob.download(0)
    return await streamToString(content.readableStreamBody)
}

/**
 * Store a user's preferences.
 * @param userName The username
 * @param preferences The preferences to store.
 */
export const StoreUserPreferences = async (userName, preferences) => {
    var userBlob = userPrefsContainer.getBlockBlobClient(`${userName}.prefs.json`)
    var data = JSON.stringify(preferences)
    await userBlob.upload(data, data.length)
}

/**
 * Get all stored team updates for a team
 * @param teamNumber The team number to get updates for
 */
export const GetTeamUpdates = async (teamNumber) => {
    var userBlob = teamUpdatesContainer.getBlockBlobClient(`${teamNumber}.json`)
    var content = await userBlob.download(0)
    return await streamToString(content.readableStreamBody)
}

/**
 * Store a team update blob
 * @param teamNumber the team number
 * @param data the update data to store
 */
export const StoreTeamUpdates = async (teamNumber, data) => {
    var userBlob = teamUpdatesContainer.getBlockBlobClient(`${teamNumber}.json`)
    var d = JSON.stringify(data)
    await userBlob.upload(d, d.length)
}

export const StoreHighScores = async (year, type, level, match) => {
    var scoreBlob = highScoresContainer.getBlockBlobClient(`${year}-${type}-${level}.json`)
    var item = {
        yearType: year + type + level,
        year: year,
        type: type,
        level: level,
        matchData: match
    }
    var d = JSON.stringify(item)
    await scoreBlob.upload(d, d.length)
}

export const GetHighScores = async (year) => {
    var iterator = highScoresContainer.listBlobsFlat({
        prefix: year
    }).byPage({ maxPageSize: 1000 })
    let response = (await iterator.next()).value;
    let r = []
    for (const blob of response.segment.blobItems) {
        var b = highScoresContainer.getBlockBlobClient(blob.name)
        var c = await b.download(0)
        r = r.concat(JSON.parse(await streamToString(c.readableStreamBody)))
    }
    return r
}

const streamToString = (stream) => {
    const chunks = [];
    return new Promise((resolve, reject) => {
        stream.on('data', (chunk) => chunks.push(Buffer.from(chunk)))
        stream.on('error', (err) => reject(err))
        stream.on('end', () => resolve(Buffer.concat(chunks).toString('utf8')))
    })
}