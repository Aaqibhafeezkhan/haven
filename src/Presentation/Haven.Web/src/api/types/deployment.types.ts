export type DeploymentStatus = 'Queued' | 'Building' | 'Starting' | 'Succeeded' | 'Failed' | 'Cancelled';
export interface DeploymentDto {
  id: string;
  serviceId: string;
  startedAt: string;
  finishedAt?: string;
  status: DeploymentStatus;
  triggeredBy?: string;
}
