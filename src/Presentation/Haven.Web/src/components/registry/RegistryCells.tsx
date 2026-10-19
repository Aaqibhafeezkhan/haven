import { ArrowRight, Globe } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import type { ServiceRegistryEntryDto } from '@/api/types';
import { Stack } from '@/components/layout';
import { CopyButton } from '@/components/ui/CopyButton';
import styles from '@/styles/components/registry/Registry.module.css';

import { buildAddresses, domainUrl, entryContext } from './registryUtils';

const MAX_VISIBLE_ADDRESSES = 3;

export function EntryName({ entry }: { entry: ServiceRegistryEntryDto }) {
  const { t } = useTranslation('serviceRegistry');
  const name = entry.containerName ?? t('none');
  const context = entryContext(entry);
  const owner = entry.sidecarName
    ? t('sidecarLabel', { name: entry.sidecarName })
    : entry.serviceName && entry.serviceName !== entry.containerName
      ? entry.serviceName
      : undefined;

  return (
    <div className={`${styles.nameCell} ${styles.copyable}`}>
      <span className={styles.nameLine}>
        <span className={styles.containerName}>{name}</span>
        {entry.containerName && (
          <CopyButton
            value={entry.containerName}
            label={t('copy.containerName')}
            className={styles.copyButton}
          />
        )}
      </span>
      {(context || owner) && (
        <span className={styles.context}>{[context, owner].filter(Boolean).join(' · ')}</span>
      )}
    </div>
  );
}

export function DomainLinks({ entry }: { entry: ServiceRegistryEntryDto }) {
  const { t } = useTranslation('serviceRegistry');

  if (!entry.domains.length) return <span className={styles.muted}>{t('none')}</span>;

  return (
    <Stack gap="1">
      {entry.domains.map(d => (
        <span key={d.id} className={styles.copyable}>
          <a
            href={domainUrl(d)}
            target="_blank"
            rel="noreferrer"
            className={styles.domainLink}
            title={t('domains.open', { hostname: d.hostname })}
            onClick={e => e.stopPropagation()}
          >
            <Globe size={13} />
            <span>{d.hostname}</span>
            {d.tlsMode !== 'None' && <span className={styles.tlsTag}>HTTPS</span>}
          </a>
          <CopyButton value={d.hostname} label={t('copy.hostname')} className={styles.copyButton} />
        </span>
      ))}
    </Stack>
  );
}

export function AddressLinks({ entry }: { entry: ServiceRegistryEntryDto }) {
  const { t } = useTranslation('serviceRegistry');
  const [expanded, setExpanded] = useState(false);
  const addresses = buildAddresses(entry);

  if (!addresses.length) return <span className={styles.muted}>{t('none')}</span>;

  const visible = expanded ? addresses : addresses.slice(0, MAX_VISIBLE_ADDRESSES);
  const hidden = addresses.length - visible.length;

  return (
    <Stack gap="1">
      {visible.map(a => (
        <span key={a.key} className={styles.copyable}>
          <a
            href={a.href}
            target="_blank"
            rel="noreferrer"
            className={styles.addressLink}
            onClick={e => e.stopPropagation()}
          >
            {a.label}
            {a.target != null && (
              <span className={styles.portArrow}>
                <ArrowRight size={11} />:{a.target}
              </span>
            )}
          </a>
          <CopyButton value={a.label} label={t('copy.address')} className={styles.copyButton} />
        </span>
      ))}
      {addresses.length > MAX_VISIBLE_ADDRESSES && (
        <button
          type="button"
          className={styles.moreButton}
          onClick={e => {
            e.stopPropagation();
            setExpanded(v => !v);
          }}
        >
          {expanded ? t('addresses.showLess') : t('addresses.more', { count: hidden })}
        </button>
      )}
    </Stack>
  );
}
