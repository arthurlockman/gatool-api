import mailchimp, { lists } from '@mailchimp/mailchimp_marketing';
import { ReadSecret } from './secretUtils';

mailchimp.setConfig({
  apiKey: await ReadSecret('MailchimpAPIKey'),
  server: await ReadSecret('MailchimpAPIURL')
});

export const GetSubscribedUsers = async () => {
  const listId = await ReadSecret('MailchimpListID');
  let members: lists.MembersSuccessResponse[] = [];
  let offset = 0;
  while (true) {
    const newMembers = (await mailchimp.lists.getListMembersInfo(listId, {
      offset,
      count: 100
    })) as lists.ListMembersInfoSuccessResponse;
    if (newMembers.members.length === 0) break;
    members = members.concat(newMembers.members);
    offset += newMembers.members.length;
  }
  return members;
};
