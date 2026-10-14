import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { Stack } from '@/components/layout';
import { parseEnvFile } from '@/lib/envFile';
import styles from '@/styles/components/services/CommandArgsEditor.module.css';

import { FormGroup, FormLabel } from '../ui/Form';

interface BuildArgsEditorProps {
  buildArgs: Record<string, string>;
  onChange: (args: Record<string, string>) => void;
  disabled?: boolean;
}

export function BuildArgsEditor({ buildArgs, onChange, disabled }: BuildArgsEditorProps) {
  const { t } = useTranslation('services');
  const [pasteOpen, setPasteOpen] = useState(false);
  const [pasteText, setPasteText] = useState('');

  const entries = Object.entries(buildArgs);
  const parsedArgs = parseEnvFile(pasteText);
  const parsedCount = Object.keys(parsedArgs).length;

  const importArgs = (mode: 'merge' | 'replace') => {
    if (parsedCount === 0) return;
    onChange(mode === 'merge' ? { ...buildArgs, ...parsedArgs } : parsedArgs);
    setPasteText('');
    setPasteOpen(false);
  };

  const updateKey = (idx: number, key: string) => {
    onChange(Object.fromEntries(entries.map(([k, v], i) => (i === idx ? [key, v] : [k, v]))));
  };

  const updateValue = (idx: number, value: string) => {
    onChange(Object.fromEntries(entries.map(([k, v], i) => [k, i === idx ? value : v])));
  };

  const removeArg = (idx: number) => {
    onChange(Object.fromEntries(entries.filter((_, i) => i !== idx)));
  };

  const addArg = () => {
    if ('' in buildArgs) return;
    onChange({ ...buildArgs, '': '' });
  };

  return (
    <FormGroup>
      <div className={styles.labelWithHelp}>
        <FormLabel htmlFor="buildArgs">{t('createPage.buildArgs')}</FormLabel>
        <span className={styles.helpText}>{t('createPage.buildArgsHelp')}</span>
      </div>
      <Stack gap="3" className={styles.argsContainer}>
        {entries.length === 0 ? (
          <p className={styles.emptyState}>{t('createPage.noBuildArgs')}</p>
        ) : (
          entries.map(([key, value], idx) => (
            <div key={idx} className={styles.argRow}>
              <input
                type="text"
                className={styles.argInput}
                placeholder={t('createPage.buildArgKeyPlaceholder')}
                value={key}
                onChange={e => updateKey(idx, e.target.value)}
                disabled={disabled}
              />
              <input
                type="text"
                className={styles.argInput}
                placeholder={t('createPage.buildArgValuePlaceholder')}
                value={value}
                onChange={e => updateValue(idx, e.target.value)}
                disabled={disabled}
              />
              <button
                type="button"
                className={styles.argRemove}
                onClick={() => removeArg(idx)}
                disabled={disabled}
              >
                ×
              </button>
            </div>
          ))
        )}
      </Stack>
      <div className={styles.actions}>
        <button
          type="button"
          className={styles.addArgButton}
          onClick={addArg}
          disabled={disabled || '' in buildArgs}
        >
          {t('createPage.addBuildArg')}
        </button>
        <button
          type="button"
          className={styles.addArgButton}
          onClick={() => setPasteOpen(o => !o)}
          disabled={disabled}
        >
          {t('createPage.fromEnvFile')}
        </button>
      </div>
      {pasteOpen && (
        <div className={styles.pastePanel}>
          <textarea
            className={styles.pasteInput}
            placeholder={t('createPage.fromEnvFilePlaceholder')}
            value={pasteText}
            onChange={e => setPasteText(e.target.value)}
            disabled={disabled}
            rows={4}
            autoFocus
          />
          <span className={styles.helpText}>
            {t('createPage.fromEnvFilePreview', { count: parsedCount })}
          </span>
          <div className={styles.actions}>
            <button
              type="button"
              className={styles.addArgButton}
              onClick={() => importArgs('merge')}
              disabled={disabled || parsedCount === 0}
            >
              {t('createPage.fromEnvFileMerge')}
            </button>
            <button
              type="button"
              className={styles.addArgButton}
              onClick={() => importArgs('replace')}
              disabled={disabled || parsedCount === 0}
            >
              {t('createPage.fromCommandReplace')}
            </button>
          </div>
        </div>
      )}
    </FormGroup>
  );
}
