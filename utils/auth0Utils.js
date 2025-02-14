import got from "got"
import {GetAuth0AdminTokens} from "./secretUtils.js"
import NodeCache from "node-cache"

const bearerTokenCache = new NodeCache()
const cacheKey = 'bearerToken'

const GetBearerToken = async () => {
    let token = bearerTokenCache.get(cacheKey)
    if (!token) {
        const machineTokens = await GetAuth0AdminTokens();
        const response = await got.post('https://gatool.auth0.com/oauth/token',
            {
                form: {
                    grant_type: 'client_credentials',
                    client_id: machineTokens.client_id,
                    client_secret: machineTokens.client_secret,
                    audience: 'https://gatool.auth0.com/api/v2/'
                }
            })
        const tmp = JSON.parse(response.body)
        bearerTokenCache.set(cacheKey, tmp.access_token, tmp.expires_in)
        token = tmp.access_token
    }
    return token
}

export const GetUser = async (email) => {
    const token = await GetBearerToken()
    const response = await got.get(`https://gatool.auth0.com/api/v2/users-by-email?email=${email}`, {
        headers: {
            'Authorization': 'Bearer ' + token,
            'Accept': 'application/json'
        }
    })
    return JSON.parse(response.body).filter(user => user.identities.filter(id => id.provider === "email").length > 0)[0]
}

export const GetUserRoles = async (userId) => {
    const token = await GetBearerToken()
    const response = await got.get(`https://gatool.auth0.com/api/v2/users/${userId}/roles`, {
        headers: {
            'Authorization': 'Bearer ' + token,
            'Accept': 'application/json'
        }
    })
    return JSON.parse(response.body)
}

export const AssignUserRoles = async (userId, roles) => {
    const token = await GetBearerToken()
    await got.post(`https://gatool.auth0.com/api/v2/users/${userId}/roles`, {
        headers: {
            'Authorization': 'Bearer ' + token,
            'Accept': 'application/json'
        },
        json: {
            "roles": roles
        }
    })
}

export const RemoveUserRoles = async (userId, roles) => {
    const token = await GetBearerToken()
    await got.delete(`https://gatool.auth0.com/api/v2/users/${userId}/roles`, {
        headers: {
            'Authorization': 'Bearer ' + token,
            'Accept': 'application/json'
        },
        json: {
            "roles": roles
        }
    })
}

export const CreateUser = async (email) => {
    const token = await GetBearerToken()
    await got.post(`https://gatool.auth0.com/api/v2/users`, {
        headers: {
            'Authorization': 'Bearer ' + token,
            'Accept': 'application/json',
            'Content-Type': 'application/json'
        },
        json: {
            email: email,
            email_verified: true,
            connection: 'email'
        }
    })
}