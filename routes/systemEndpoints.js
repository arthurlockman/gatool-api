import express from 'express'
import {GetAnnouncements, StoreAnnouncements} from "../utils/storageUtils.js";
import {AssignUserRoles, CreateUser, GetUser, GetUserRoles, RemoveUserRoles} from "../utils/auth0Utils.js";
import {GetSubscribedUsers} from "../utils/mailChimpUtils.js";
import logger from "../logger.js";

export var router = express.Router()

// Announcement storage
router.get('/announcements', async (_, res) => {
    res.setHeader('Cache-Control', 'no-cache')
    try {
        var prefs = await GetAnnouncements()
        res.json(JSON.parse(prefs))
    } catch (e) {
        console.error(e)
        res.status(404).send()
    }
})

router.put('/announcements', async (req, res) => {
    // @ts-ignore
    if (req.auth.payload['https://gatool.org/roles'].includes('admin')) {
        await StoreAnnouncements(req.body)
        res.status(204).send()
    }
    res.status(403).send()
})

router.get('/admin/users/:email', async (req, res) => {
    res.setHeader('Cache-Control', 'no-cache')
    if (req.auth.payload['https://gatool.org/roles'].includes('admin')) {
        let user = await GetUser(req.params.email)
        user.roles = await GetUserRoles(user.user_id)
        res.json(user)
    }
    res.status(403).send()
})

router.post('/admin/users', async (req, res) => {
    if (req.auth.payload['https://gatool.org/roles'].includes('admin')) {
        await CreateUser(req.body.email)
        res.status(204).send()
    }
    res.status(403).send()
})

router.post('/admin/users/:email/roles', async (req, res) => {
    if (req.auth.payload['https://gatool.org/roles'].includes('admin')) {
        const roles = req.body.roles
        const user = await GetUser(req.params.email)
        await AssignUserRoles(user.user_id, roles)
        res.status(204).send()
    }
    res.status(403).send()
})

router.post('/admin/syncusers', async (req, res) => {
    const optInText = "I want access to gatool and agree that I will not abuse this access to team data."
    if (req.auth.payload['https://gatool.org/roles'].includes('admin')) {
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
        }
        const optedOutUsers = listMembers.filter(member =>
            member.status === "unsubscribed" || member.merge_fields.GATOOL !== optInText
        ).map(member => member.email_address.toLocaleLowerCase())
        logger.info(`There are ${optedOutUsers.length} users opted out or unsubscribed. Assigning viewer role and removing user role.`)
        for (const email of optedOutUsers) {
            let user = await GetUser(email)
            if (user == null) {
                logger.info(`User ${email} does not exist, skipping...`)
            } else {
                await RemoveUserRoles(email, ['rol_KRLODHx3eNItUgvI'])
                await AssignUserRoles(email, ['rol_EQcREtmOWaGanRYG'])
                logger.info(`Removed editing permissions from user ${email}.`)
            }
        }
        res.status(204).send()
    }
    res.status(403).send()
})