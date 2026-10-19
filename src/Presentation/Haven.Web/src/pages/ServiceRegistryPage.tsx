import { Globe, Pause, Play, RefreshCw, Server } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { Row } from '@/components/layout';
import { RegistryDomainsTab } from '@/components/registry/RegistryDomainsTab';
import { RegistryEntriesTab } from '@/components/registry/RegistryEntriesTab';
import { RegistrySummary } from '@/components/registry/RegistrySummary';
import { Button } from '@/components/ui/Button';
import { Tabs } from '@/components/ui/Tabs';
import { useSetBreadcrumbs } from '@/hooks/useSetBreadcrumbs';
import { useSetUrlParams, useUrlState } from '@/hooks/useUrlState';
import styles from '@/styles/pages/ServiceRegistryPage.module.css';

const REFRESH_INTERVAL_MS = 15_000;

export function ServiceRegistryPage() {
  const { t } = useTranslation('serviceRegistry');
  const [tab, setTab] = useUrlState('tab', 'services');
  const [status] = useUrlState('status', '');
  const [exposure] = useUrlState('exposure', '');
  const setUrlParams = useSetUrlParams();

  const [autoRefresh, setAutoRefresh] = useState(true);
  const [refreshTick, setRefreshTick] = useState(0);

  useSetBreadcrumbs([{ label: t('title') }]);

  useEffect(() => {
    if (!autoRefresh) return;
    const tick = () => {
      if (!document.hidden) setRefreshTick(n => n + 1);
    };
    const id = setInterval(tick, REFRESH_INTERVAL_MS);
    // Catch up right away when the user comes back to the tab.
    document.addEventListener('visibilitychange', tick);
    return () => {
      clearInterval(id);
      document.removeEventListener('visibilitychange', tick);
    };
  }, [autoRefresh]);

  return (
    <div className={styles.container}>
      <div className={styles.headerRow}>
        <div className={styles.header}>
          <h1 className={styles.title}>{t('title')}</h1>
          <p className={styles.subtitle}>{t('subtitle')}</p>
        </div>
        <Row gap="2" align="center">
          <Button
            variant="ghost"
            size="sm"
            icon={<RefreshCw size={14} />}
            onClick={() => setRefreshTick(n => n + 1)}
          >
            {t('refresh.now')}
          </Button>
          <Button
            variant="secondary"
            size="sm"
            icon={autoRefresh ? <Pause size={14} /> : <Play size={14} />}
            aria-pressed={autoRefresh}
            title={autoRefresh ? t('refresh.pauseHint') : t('refresh.resumeHint')}
            onClick={() => setAutoRefresh(v => !v)}
          >
            {autoRefresh ? t('refresh.live') : t('refresh.paused')}
          </Button>
        </Row>
      </div>

      <RegistrySummary
        refreshTick={refreshTick}
        status={status}
        exposure={exposure}
        onStatusClick={value => {
          setUrlParams({ status: value, exposure: '', project: '', env: '', tab: '' });
        }}
        onExposedClick={() => {
          setUrlParams({ status: '', exposure: 'External', tab: '' });
        }}
        onDomainsClick={() => setTab('domains')}
      />

      <Tabs
        activeTab={tab}
        onChange={setTab}
        items={[
          {
            id: 'services',
            label: t('tabs.services'),
            icon: <Server size={14} />,
            content: <RegistryEntriesTab refreshTick={refreshTick} />,
          },
          {
            id: 'domains',
            label: t('tabs.domains'),
            icon: <Globe size={14} />,
            content: <RegistryDomainsTab refreshTick={refreshTick} />,
          },
        ]}
      />
    </div>
  );
}
