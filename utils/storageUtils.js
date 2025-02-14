import {ReadSecret} from "./secretUtils.js"
import {BlobServiceClient} from '@azure/storage-blob'

const blobStorageConnectionString = await ReadSecret('UserStorageConnectionString')
const blobServiceClient = BlobServiceClient.fromConnectionString(
    blobStorageConnectionString
)

const userPrefsContainer = blobServiceClient.getContainerClient('gatool-user-preferences')
const teamUpdatesContainer = blobServiceClient.getContainerClient('gatool-team-updates')
const teamUpdateHistoryContainer = blobServiceClient.getContainerClient('gatool-team-updates-history')
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
 * Get stored system announcements
 */
export const GetAnnouncements = async () => {
    var userBlob = userPrefsContainer.getBlockBlobClient(`system.announce.json`)
    var content = await userBlob.download(0)
    return await streamToString(content.readableStreamBody)
}

/**
 * Store system announcements
 * @param announcements The announcements to store.
 */
export const StoreAnnouncements = async (announcements) => {
    var userBlob = userPrefsContainer.getBlockBlobClient(`system.announce.json`)
    var data = JSON.stringify(announcements)
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
 * Get all historical update versions for a team
 * @param teamNumber The team number to get update history for
 */
export const GetTeamUpdateHistory = async (teamNumber) => {
    var iterator = teamUpdateHistoryContainer.listBlobsFlat({
        prefix: `${teamNumber}/`
    }).byPage({maxPageSize: 1000})
    let response = (await iterator.next()).value
    let r = []
    for (const blob of response.segment.blobItems) {
        var b = teamUpdateHistoryContainer.getBlockBlobClient(blob.name)
        var c = await b.download(0)
        var u = JSON.parse(await streamToString(c.readableStreamBody))
        u.modifiedDate = blob.name.replace(`${teamNumber}/`, '').replace(`.json`, '')
        r = r.concat(u)
    }
    return r
}

/**
 * Store a team update blob
 * @param teamNumber the team number
 * @param data the update data to store
 * @param email the email of the user making the update
 */
export const StoreTeamUpdates = async (teamNumber, data, email) => {
    try {
        const blob = teamUpdatesContainer.getBlockBlobClient(`${teamNumber}.json`)
        const lastModifiedDate = (await blob.getProperties()).lastModified
        const b = await blob.download(0)
        const content = await streamToString(b.readableStreamBody)
        const historyBlob = teamUpdateHistoryContainer.getBlockBlobClient(`${teamNumber}/${lastModifiedDate.toJSON()}.json`)
        await historyBlob.upload(content, content.length)
    } catch {
        // No stored updates, continue without saving history
    }
    var userBlob = teamUpdatesContainer.getBlockBlobClient(`${teamNumber}.json`)
    data.source = email
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
    }).byPage({maxPageSize: 1000})
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