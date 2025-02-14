import {GetSubscribedUsers} from "./mailChimpUtils.js";
import logger from "../logger.js";
import {AssignUserRoles, CreateUser, DeleteUser, GetUser, RemoveUserRoles} from "./auth0Utils.js";

const optInText = "I want access to gatool and agree that I will not abuse this access to team data."

const delay = async (ms) =>  {
    await new Promise(resolve => setTimeout(resolve, ms))
}

export const SyncUsers = async () => {
    const listMembers = await GetSubscribedUsers()
    const optedInUsers = listMembers.filter(member =>
        member.status === "subscribed" && member.merge_fields.GATOOL === optInText
    ).map(member => member.email_address.toLocaleLowerCase())
    logger.info(`There are ${optedInUsers.length} users opted in. Assigning user role.`)
    for (const email of optedInUsers) {
        let user = await GetUser(email)
        if (user == null) {
            // If user doesn't exist we have to create them
            logger.info(`User ${email} does not exist, creating...`)
            await CreateUser(email)
            user = await GetUser(email)
        }
        logger.info(`Assigning 'user' role to ${email}...`)
        await AssignUserRoles(user.user_id, ['rol_KRLODHx3eNItUgvI'])
        // auth0 has an annoying rate limit on the management API so we have to slow the process down
        await delay(500)
    }
    const optedOutUsers = listMembers.filter(member =>
        member.status === "subscribed" && member.merge_fields.GATOOL !== optInText
    ).map(member => member.email_address.toLocaleLowerCase())
    logger.info(`There are ${optedOutUsers.length} users opted out. Assigning viewer role and removing user role.`)
    for (const email of optedOutUsers) {
        let user = await GetUser(email)
        if (user == null) {
            logger.info(`User ${email} does not exist, creating...`)
            await CreateUser(email)
            user = await GetUser(email)
        }
        await RemoveUserRoles(user.user_id, ['rol_KRLODHx3eNItUgvI'])
        await AssignUserRoles(user.user_id, ['rol_EQcREtmOWaGanRYG'])
        logger.info(`Removed editing permissions from user ${email}.`)
        await delay(500)
    }
    const unsubscribedUsers = listMembers.filter(member =>
        member.status === "unsubscribed"
    ).map(member => member.email_address.toLocaleLowerCase())
    logger.info(`There are ${unsubscribedUsers.length} users unsubscribed. Deleting accounts.`)
    for (const email of unsubscribedUsers) {
        let user = await GetUser(email)
        if (user == null) {
            logger.info(`User ${email} does not exist. No need to delete.`)
        } else {
            await DeleteUser(user.user_id)
            logger.info(`Deleted user ${email}.`)
        }
        await delay(500)
    }
}