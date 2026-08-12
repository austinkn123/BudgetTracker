import type { ReactNode } from 'react';
import * as RadixAvatar from '@radix-ui/react-avatar';
import { cn } from '../../utils/cn';

export interface AvatarProps {
  /** Initials or an icon. */
  children: ReactNode;
  size?: number;
  className?: string;
}

/** Initials avatar (BUD-20). No image sources in the app yet — fallback only. */
const Avatar = ({ children, size = 36, className }: AvatarProps) => (
  <RadixAvatar.Root
    className={cn(
      'inline-flex select-none items-center justify-center overflow-hidden rounded-full bg-primary-subtle text-[13px] font-semibold text-primary-dark',
      className,
    )}
    style={{ width: size, height: size }}
  >
    <RadixAvatar.Fallback delayMs={0}>{children}</RadixAvatar.Fallback>
  </RadixAvatar.Root>
);

export default Avatar;
