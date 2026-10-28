import { Info } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import type { ResourceLimitsErrors, ResourceLimitsForm } from '@/lib/resourceLimits';
import styles from '@/styles/components/services/ResourceLimitsFields.module.css';

import { FormGroup, FormInput, FormLabel } from '../ui/Form';
import { Tooltip } from '../ui/Tooltip';

interface FieldConfig {
  field: keyof ResourceLimitsForm;
  labelKey:
    | 'resourceLimits.memoryLimit'
    | 'resourceLimits.memorySwap'
    | 'resourceLimits.memoryReservation'
    | 'resourceLimits.cpus';
  hintKey:
    | 'resourceLimits.memoryLimitHint'
    | 'resourceLimits.memorySwapHint'
    | 'resourceLimits.memoryReservationHint'
    | 'resourceLimits.cpusHint';
  unitKey: 'resourceLimits.unitMib' | 'resourceLimits.unitCores';
  placeholderKey:
    | 'resourceLimits.placeholderUnlimited'
    | 'resourceLimits.placeholderDefault'
    | 'resourceLimits.placeholderNone';
}

const FIELDS: FieldConfig[] = [
  {
    field: 'memoryLimit',
    labelKey: 'resourceLimits.memoryLimit',
    hintKey: 'resourceLimits.memoryLimitHint',
    unitKey: 'resourceLimits.unitMib',
    placeholderKey: 'resourceLimits.placeholderUnlimited',
  },
  {
    field: 'memorySwap',
    labelKey: 'resourceLimits.memorySwap',
    hintKey: 'resourceLimits.memorySwapHint',
    unitKey: 'resourceLimits.unitMib',
    placeholderKey: 'resourceLimits.placeholderDefault',
  },
  {
    field: 'memoryReservation',
    labelKey: 'resourceLimits.memoryReservation',
    hintKey: 'resourceLimits.memoryReservationHint',
    unitKey: 'resourceLimits.unitMib',
    placeholderKey: 'resourceLimits.placeholderNone',
  },
  {
    field: 'cpus',
    labelKey: 'resourceLimits.cpus',
    hintKey: 'resourceLimits.cpusHint',
    unitKey: 'resourceLimits.unitCores',
    placeholderKey: 'resourceLimits.placeholderUnlimited',
  },
];

interface ResourceLimitsFieldsProps {
  value: ResourceLimitsForm;
  onChange: (value: ResourceLimitsForm) => void;
  errors: ResourceLimitsErrors;
  disabled?: boolean;
}

export function ResourceLimitsFields({
  value,
  onChange,
  errors,
  disabled,
}: ResourceLimitsFieldsProps) {
  const { t } = useTranslation('services');

  const renderField = (config: FieldConfig) => {
    const { field, labelKey, hintKey, unitKey, placeholderKey } = config;
    const label = `${t(labelKey)} (${t(unitKey)})`;
    const hint = t(hintKey);
    const id = `resourceLimits-${field}`;
    const error = errors[field];

    return (
      <FormGroup key={field}>
        <div className={styles.labelRow}>
          <FormLabel htmlFor={id} optional>
            {label}
          </FormLabel>
          <Tooltip content={hint} direction="above" wrap>
            <Info size={13} className={styles.hintIcon} aria-label={hint} />
          </Tooltip>
        </div>
        <FormInput
          id={id}
          type="number"
          inputMode="decimal"
          step="any"
          placeholder={t(placeholderKey)}
          value={value[field]}
          onChange={e => onChange({ ...value, [field]: e.target.value })}
          error={error ? t(`resourceLimits.errors.${error}`) : undefined}
          disabled={disabled}
        />
      </FormGroup>
    );
  };

  return (
    <div className={styles.configFields}>
      <h3 className={styles.configTitle}>{t('resourceLimits.title')}</h3>
      <p className={styles.description}>{t('resourceLimits.description')}</p>
      <div className={styles.grid}>{FIELDS.map(renderField)}</div>
    </div>
  );
}
