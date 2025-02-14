import mailchimp from "@mailchimp/mailchimp_marketing"
import {ReadSecret} from "./secretUtils.js"

mailchimp.setConfig({
    apiKey: await ReadSecret("MailchimpAPIKey"),
    server: await ReadSecret("MailchimpAPIURL"),
})

export const GetSubscribedUsers = async () => {
    const listId = await ReadSecret("MailchimpListID")
    let members = []
    let offset = 0
    while (true) {
        const newMembers = await mailchimp.lists.getListMembersInfo(listId, {
            offset: offset,
            count: 100,
        })
        if (newMembers.members.length === 0) break
        members = members.concat(newMembers.members)
        offset += newMembers.members.length
    }
    return members
}