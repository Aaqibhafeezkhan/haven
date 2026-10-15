import { ArrowRight } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';

import type { ServiceRegistryEntryDto } from '@/api/types';
import { Button } from '@/components/ui/Button';
import { CopyButton } from '@/components/ui/CopyButton';
import styles from '@/styles/components/registry/Registry.module.css';

import { canOpenService, formatUptime, portHost } from './registryUtils';

const formatDate = (iso?: string) => (iso ? new Date(iso).toLocaleString() : '—');

export function RegistryEntryDetails({ entry }: { entry: ServiceRegistryEntryDto }) {
  const { t } = useTranslation('serviceRegistry');
  const navigate = useNavigate();

  return (
    <div className={styles.details}>
      <section className={styles.detailSection}>
        <h3 className={styles.detailTitle}>{t('details.ports')}</h3>
        {entry.ports.length ? (
          <table className={styles.detailTable}>
            <thead>
              <tr>
                <th>{t('details.containerPort')}</th>
                <th>{t('details.published')}</th>
              </tr>
            </thead>
            <tbody>
              {entry.ports.map((p, i) => (
                <tr key={i}>
                  <td>{p.containerPort}</td>
                  <td>
                    {p.hostPort ? (
                      `${portHost(p.ipAddress)}:${p.hostPort}`
                    ) : (
                      <span className={styles.muted}>{t('details.notPublished')}</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : (
          <span className={styles.muted}>{t('none')}</span>
        )}
      </section>

      <section className={styles.detailSection}>
        <h3 className={styles.detailTitle}>{t('details.network')}</h3>
        <dl className={styles.detailList}>
          <dt>{t('details.containerIp')}</dt>
          <dd className={styles.copyable}>
            {entry.ipAddress ?? '—'}
            {entry.ipAddress && (
              <CopyButton
                value={entry.ipAddress}
                label={t('copy.address')}
                className={styles.copyButton}
              />
            )}
          </dd>
          <dt>{t('details.domains')}</dt>
          <dd>
            {entry.domains.length ? (
              entry.domains.map(d => (
                <div key={d.id}>
                  {d.hostname} → :{d.containerPort}
                  {d.internalBasePath ? d.internalBasePath : ''} ·{' '}
                  {d.tlsMode === 'None'
                    ? t('details.tlsOff')
                    : d.certificateName
                      ? t('details.tlsCert', { name: d.certificateName })
                      : t('details.tlsMode', { mode: d.tlsMode })}
                </div>
              ))
            ) : (
              <span className={styles.muted}>{t('none')}</span>
            )}
          </dd>
        </dl>
      </section>

      <section className={styles.detailSection}>
        <h3 className={styles.detailTitle}>{t('details.timeline')}</h3>
        <dl className={styles.detailList}>
          <dt>{t('details.started')}</dt>
          <dd>
            {entry.startedAt
              ? `${formatDate(entry.startedAt)} (${formatUptime(entry.startedAt)})`
              : '—'}
          </dd>
          <dt>{t('details.registered')}</dt>
          <dd>{formatDate(entry.registeredAt)}</dd>
          <dt>{t('details.updated')}</dt>
          <dd>{formatDate(entry.updatedAt)}</dd>
        </dl>
        {canOpenService(entry.serviceId) && (
          <Button
            variant="secondary"
            size="sm"
            icon={<ArrowRight size={14} />}
            onClick={() => navigate(`/services/${entry.serviceId}`)}
          >
            {t('openService')}
          </Button>
        )}
      </section>
    </div>
  );
}
