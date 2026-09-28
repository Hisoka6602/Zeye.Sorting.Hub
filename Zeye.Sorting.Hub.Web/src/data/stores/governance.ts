import { initialArchiveTasks, initialOutboxMessages } from '../mock/governance';
import { createStoredList } from './createStoredList';

export const useArchiveTasks = createStoredList('zeye-demo-archive', initialArchiveTasks);
export const useOutboxMessages = createStoredList('zeye-demo-outbox', initialOutboxMessages);
