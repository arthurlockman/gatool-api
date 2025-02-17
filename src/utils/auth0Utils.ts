import {GetAuth0AdminTokens} from "./secretUtils";
import {ManagementClient} from 'auth0';

const machineTokens = await GetAuth0AdminTokens();
const auth0ApiClient = new ManagementClient({
    domain: 'gatool.auth0.com',
    clientId: machineTokens.client_id,
    clientSecret: machineTokens.client_secret,
});

export const GetUser = async (email: string) => {
    const apiResponse = await auth0ApiClient.usersByEmail.getByEmail({
        email,
    })
    if (apiResponse.status === 200) {
        return apiResponse.data.filter(user =>
            user.identities.filter(id => id.provider === "email").length > 0)[0]
    } else {
        return null
    }
}

export const AssignUserRoles = async (userId: string, roles: string[]) => {
    await auth0ApiClient.users.assignRoles({id: userId}, {roles});
}

export const RemoveUserRoles = async (userId: string, roles: string[]) => {
    await auth0ApiClient.users.deleteRoles({id: userId}, {roles});
}

export const CreateUser = async (email: string) => {
    await auth0ApiClient.users.create({
        email,
        email_verified: true,
        connection: 'email'
    });
}

export const DeleteUser = async (userId: string) => {
    await auth0ApiClient.users.delete({id: userId});
}