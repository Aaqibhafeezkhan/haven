import { clsx } from 'clsx';
import {
  Activity,
  AlertCircle,
  Boxes,
  CheckCircle2,
  FolderMinus,
  FolderOpen,
  FolderPlus,
  HeartPulse,
  Layers,
  Link2,
  Loader2,
  LucideIcon,
  Network,
  PackagePlus,
  PenLine,
  RefreshCw,
  Repeat2,
  Rocket,
  Server,
  Settings,
  StopCircle,
  ToggleLeft,
  ToggleRight,
  Trash2,
  Unlink2,
  UserPlus,
  Variable,
} from 'lucide-react';

import styles from '@/styles/components/ui/EventIcon.module.css';

import { Badge } from './Badge';

export const EVENT_TYPES = {
  EnvironmentCreated: 'EnvironmentCreatedEvent',
  EnvironmentDeleted: 'EnvironmentDeletedEvent',
  EnvironmentUpdated: 'EnvironmentUpdatedEvent',
  EnvironmentVariablesUpdated: 'EnvironmentVariablesUpdatedEvent',
  ProjectCreated: 'ProjectCreatedEvent',
  ProjectDeleted: 'ProjectDeletedEvent',
  ProjectUpdated: 'ProjectUpdatedEvent',
  ServiceCrashed: 'ServiceCrashedEvent',
  ServiceCreated: 'ServiceCreatedEvent',
  ServiceDegraded: 'ServiceDegradedEvent',
  ServiceDeleted: 'ServiceDeletedEvent',
  ServiceDeployed: 'ServiceDeployedEvent',
  ServiceDeploying: 'ServiceDeployingEvent',
  ServiceHealthChanged: 'ServiceHealthChangedEvent',
  ServiceRecovered: 'ServiceRecoveredEvent',
  ServiceRuntimeStateChanged: 'ServiceRuntimeStateChangedEvent',
  ServiceRestarted: 'ServiceRestartedEvent',
  ServiceStopped: 'ServiceStoppedEvent',
  ServiceUpdated: 'ServiceUpdatedEvent',
  SidecarCreated: 'SidecarCreatedEvent',
  SidecarUpdated: 'SidecarUpdatedEvent',
  SidecarDeleted: 'SidecarDeletedEvent',
  SidecarEnabled: 'SidecarEnabledEvent',
  SidecarDisabled: 'SidecarDisabledEvent',
  SidecarAttached: 'SidecarAttachedEvent',
  SidecarDetached: 'SidecarDetachedEvent',
  NetworkCreated: 'NetworkCreatedEvent',
  NetworkDeleted: 'NetworkDeletedEvent',
  NetworkRecreated: 'NetworkRecreatedEvent',
  UserCreated: 'UserCreatedEvent',
} as const;

type EventType = (typeof EVENT_TYPES)[keyof typeof EVENT_TYPES];

interface EventConfig {
  icon: LucideIcon;
  variant: 'primary' | 'success' | 'warning' | 'danger' | 'default';
  label: string;
}

const EVENT_CONFIG: Record<EventType, EventConfig> = {
  [EVENT_TYPES.EnvironmentCreated]: {
    icon: FolderPlus,
    variant: 'success',
    label: 'Environment Created',
  },
  [EVENT_TYPES.EnvironmentDeleted]: {
    icon: FolderMinus,
    variant: 'danger',
    label: 'Environment Deleted',
  },
  [EVENT_TYPES.EnvironmentUpdated]: {
    icon: FolderOpen,
    variant: 'warning',
    label: 'Environment Updated',
  },
  [EVENT_TYPES.EnvironmentVariablesUpdated]: {
    icon: Variable,
    variant: 'warning',
    label: 'Environment Variables Updated',
  },
  [EVENT_TYPES.ProjectCreated]: {
    icon: Layers,
    variant: 'success',
    label: 'Project Created',
  },
  [EVENT_TYPES.ProjectDeleted]: {
    icon: Trash2,
    variant: 'danger',
    label: 'Project Deleted',
  },
  [EVENT_TYPES.ProjectUpdated]: {
    icon: PenLine,
    variant: 'warning',
    label: 'Project Updated',
  },
  [EVENT_TYPES.ServiceCrashed]: {
    icon: Activity,
    variant: 'danger',
    label: 'Service Crashed',
  },
  [EVENT_TYPES.ServiceCreated]: {
    icon: Server,
    variant: 'primary',
    label: 'Service Created',
  },
  [EVENT_TYPES.ServiceDegraded]: {
    icon: Activity,
    variant: 'danger',
    label: 'Service Degraded',
  },
  [EVENT_TYPES.ServiceDeleted]: {
    icon: Trash2,
    variant: 'danger',
    label: 'Service Deleted',
  },
  [EVENT_TYPES.ServiceDeployed]: {
    icon: Rocket,
    variant: 'success',
    label: 'Service Deployed',
  },
  [EVENT_TYPES.ServiceRecovered]: {
    icon: CheckCircle2,
    variant: 'success',
    label: 'Service Recovered',
  },
  [EVENT_TYPES.ServiceRestarted]: {
    icon: RefreshCw,
    variant: 'primary',
    label: 'Service Restarted',
  },
  [EVENT_TYPES.ServiceStopped]: {
    icon: StopCircle,
    variant: 'danger',
    label: 'Service Stopped',
  },
  [EVENT_TYPES.ServiceDeploying]: {
    icon: Loader2,
    variant: 'primary',
    label: 'Service Deploying',
  },
  [EVENT_TYPES.ServiceUpdated]: {
    icon: Settings,
    variant: 'warning',
    label: 'Service Updated',
  },
  [EVENT_TYPES.SidecarCreated]: {
    icon: PackagePlus,
    variant: 'success',
    label: 'Sidecar Created',
  },
  [EVENT_TYPES.SidecarUpdated]: {
    icon: Boxes,
    variant: 'warning',
    label: 'Sidecar Updated',
  },
  [EVENT_TYPES.SidecarDeleted]: {
    icon: Trash2,
    variant: 'danger',
    label: 'Sidecar Deleted',
  },
  [EVENT_TYPES.SidecarEnabled]: {
    icon: ToggleRight,
    variant: 'success',
    label: 'Sidecar Enabled',
  },
  [EVENT_TYPES.SidecarDisabled]: {
    icon: ToggleLeft,
    variant: 'default',
    label: 'Sidecar Disabled',
  },
  [EVENT_TYPES.SidecarAttached]: {
    icon: Link2,
    variant: 'primary',
    label: 'Sidecar Attached',
  },
  [EVENT_TYPES.SidecarDetached]: {
    icon: Unlink2,
    variant: 'default',
    label: 'Sidecar Detached',
  },
  [EVENT_TYPES.ServiceHealthChanged]: {
    icon: HeartPulse,
    variant: 'warning',
    label: 'Health Changed',
  },
  [EVENT_TYPES.ServiceRuntimeStateChanged]: {
    icon: Repeat2,
    variant: 'primary',
    label: 'Runtime State Changed',
  },
  [EVENT_TYPES.NetworkCreated]: {
    icon: Network,
    variant: 'success',
    label: 'Network Created',
  },
  [EVENT_TYPES.NetworkDeleted]: {
    icon: Network,
    variant: 'danger',
    label: 'Network Deleted',
  },
  [EVENT_TYPES.NetworkRecreated]: {
    icon: Network,
    variant: 'warning',
    label: 'Network Recreated',
  },
  [EVENT_TYPES.UserCreated]: {
    icon: UserPlus,
    variant: 'success',
    label: 'User Created',
  },
};

interface EventIconProps {
  type: string;
  className?: string;
}

export function EventIcon({ type, className }: EventIconProps) {
  const config = EVENT_CONFIG[type as EventType];

  if (!config) {
    return (
      <Badge variant="default" className={clsx(styles.badge, className)}>
        <AlertCircle size={16} />
      </Badge>
    );
  }

  const IconComponent = config.icon;

  return (
    <Badge variant={config.variant} className={clsx(styles.badge, className)} title={config.label}>
      <IconComponent size={16} />
    </Badge>
  );
}
