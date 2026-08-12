/**
 * Shared UI component library (BUD-16).
 *
 * Feature code imports every button, input, card, modal, table, badge, and
 * alert from here — direct MUI imports of those primitives are blocked by an
 * ESLint rule so the design system stays the single styling channel.
 */

export { default as BudAlert } from './BudAlert';
export type { BudAlertProps, BudAlertSeverity } from './BudAlert';

export { default as BudBadge } from './BudBadge';
export type { BudBadgeColor, BudBadgeProps, BudBadgeVariant } from './BudBadge';

export { default as BudButton } from './BudButton';
export type { BudButtonProps, BudButtonSize, BudButtonVariant } from './BudButton';

export { default as BudCard } from './BudCard';
export type { BudCardPadding, BudCardProps, BudCardVariant } from './BudCard';

export { default as BudConfirmModal } from './BudConfirmModal';
export type { BudConfirmModalProps } from './BudConfirmModal';

export { default as BudInput } from './BudInput';
export type { BudInputProps, BudInputType, BudInputValueAs } from './BudInput';

export { default as BudModal } from './BudModal';
export type { BudModalMaxWidth, BudModalProps } from './BudModal';

export { default as BudModalActions } from './BudModalActions';
export type { BudModalActionsProps } from './BudModalActions';

export { default as BudSelect } from './BudSelect';
export type { BudSelectOption, BudSelectProps } from './BudSelect';

export { default as BudTable } from './BudTable';
export type { BudTableColumn, BudTableProps } from './BudTable';
