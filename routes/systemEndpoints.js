import express from 'express'
import {GetAnnouncements, StoreAnnouncements} from "../utils/storageUtils.js";
import {AssignUserRoles, CreateUser, GetUser} from "../utils/auth0Utils.js";
import {SyncUsers} from "../utils/syncUsers.js";

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
    if (req.auth.payload['https://gatool.org/roles'].includes('admin')) {
        await SyncUsers()
        res.status(204).send()
    }
    res.status(403).send()
})
