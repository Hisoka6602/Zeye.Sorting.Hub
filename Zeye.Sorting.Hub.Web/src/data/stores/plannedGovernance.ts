import { initialBackupJobs, initialPartitions } from '../mock/plannedGovernance';
import { createStoredList } from './createStoredList';

export const useBackupJobs = createStoredList('zeye-demo-backup', initialBackupJobs);
export const usePartitions = createStoredList('zeye-demo-partitions', initialPartitions);
