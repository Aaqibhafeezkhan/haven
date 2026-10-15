import { clsx } from 'clsx';
import { useTranslation } from 'react-i18next';

import { serviceRegistryApi } from '@/api/serviceRegistry';
import { useRefreshableQuery } from '@/hooks/useRefreshableQuery';
import styles from '@/styles/components/registry/Registry.module.css';

interface Props {
  refreshTick: number;
  /** Currently applied status filter ('' for none) and exposure filter, used to highlight a stat. */
  status: string;
  exposure: string;
  onStatusClick: (status: '' | 'Running' | 'Stopped' | 'Starting' | 'Unknown') => void;
  onExposedClick: () => void;
  onDomainsClick: () => void;
}

export function RegistrySummary({
  refreshTick,
  status,
  exposure,
  onStatusClick,
  onExposedClick,
  onDomainsClick,
}: Props) {
  const { t } = useTranslation('serviceRegistry');
  const { data } = useRefreshableQuery(
    () => serviceRegistryApi.getSummary(),
    'summary',
    refreshTick,
    t('error')
  );

  if (!data) return null;

  const stats = [
    {
      key: 'total',
      label: t('summary.total'),
      value: data.total,
      active: !status && !exposure,
      onClick: () => onStatusClick(''),
    },
    {
      key: 'running',
      label: t('summary.running'),
      value: data.running,
      tone: styles.toneRunning,
      active: status === 'Running',
      onClick: () => onStatusClick('Running'),
    },
    {
      key: 'stopped',
      label: t('summary.stopped'),
      value: data.stopped,
      tone: styles.toneStopped,
      active: status === 'Stopped',
      onClick: () => onStatusClick('Stopped'),
    },
    {
      key: 'starting',
      label: t('summary.starting'),
      value: data.starting,
      active: status === 'Starting',
      onClick: () => onStatusClick('Starting'),
    },
    {
      key: 'unknown',
      label: t('summary.unknown'),
      value: data.unknown,
      active: status === 'Unknown',
      onClick: () => onStatusClick('Unknown'),
    },
    {
      key: 'domains',
      label: t('summary.domains'),
      value: data.domainCount,
      onClick: onDomainsClick,
    },
    {
      key: 'exposed',
      label: t('summary.exposed'),
      value: data.exposedExternally,
      active: exposure === 'External',
      onClick: onExposedClick,
    },
  ];

  return (
    <div className={styles.summary}>
      {stats.map(s => (
        <button
          key={s.key}
          type="button"
          className={clsx(styles.stat, s.active && styles.statActive)}
          aria-pressed={s.active}
          onClick={s.onClick}
        >
          <span className={clsx(styles.statValue, s.tone)}>{s.value}</span>
          <span className={styles.statLabel}>{s.label}</span>
        </button>
      ))}
    </div>
  );
}
