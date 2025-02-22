import mailchimp, { campaigns, lists } from '@mailchimp/mailchimp_marketing';
import { ReadSecret } from './secretUtils';
import logger from '../logger';

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

export const CloneAndSendWelcomeCampaign = async () => {
  const campaigns = (await mailchimp.campaigns.list({
    status: 'sent',
    sortField: 'send_time',
    sortDir: 'desc'
  })) as campaigns.CampaignsSuccessResponse;
  const mostRecentSend = campaigns.campaigns.filter((c) =>
    c.settings.subject_line.includes('Welcome to the FIRST gatool!')
  )[0];
  logger.info(`Copying campaign ${mostRecentSend.id}`);
  // @ts-expect-error the mailchimp types are missing the replicate command
  const newCampaign = (await mailchimp.campaigns.replicate(mostRecentSend.id)) as campaigns.Campaigns;
  logger.info(`Sending campaign ${newCampaign.id}`);
  await mailchimp.campaigns.send(newCampaign.id);
};
