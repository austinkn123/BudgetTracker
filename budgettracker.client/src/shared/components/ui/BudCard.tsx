import type { ReactNode } from 'react';
import Card from '@mui/material/Card';
import CardActionArea from '@mui/material/CardActionArea';
import CardContent from '@mui/material/CardContent';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import type { SxProps, Theme } from '@mui/material/styles';

export type BudCardPadding = 'none' | 'sm' | 'md' | 'lg';
export type BudCardVariant = 'outlined' | 'elevated' | 'plain';

/**
 * Padding is a named token rather than a number: MUI's spacing scale is 8px and
 * Tailwind's is 4px, so a bare `padding={2}` would be ambiguous at the API edge.
 */
const PADDING_MAP: Record<BudCardPadding, number> = {
  none: 0,
  sm: 2,
  md: 3,
  lg: 4,
};

export interface BudCardProps {
  title?: ReactNode;
  subtitle?: ReactNode;
  /** Header top-right slot, typically actions or a badge. */
  actions?: ReactNode;
  footer?: ReactNode;
  padding?: BudCardPadding;
  variant?: BudCardVariant;
  fullHeight?: boolean;
  onClick?: () => void;
  headerSx?: SxProps<Theme>;
  contentSx?: SxProps<Theme>;
  sx?: SxProps<Theme>;
  children: ReactNode;
}

const BudCard = ({
  title,
  subtitle,
  actions,
  footer,
  padding = 'md',
  variant = 'outlined',
  fullHeight = false,
  onClick,
  headerSx,
  contentSx,
  sx,
  children,
}: BudCardProps) => {
  const pad = PADDING_MAP[padding];
  const hasHeader = Boolean(title || subtitle || actions);

  const body = (
    <>
      {hasHeader && (
        <Box
          sx={{
            display: 'flex',
            alignItems: 'flex-start',
            justifyContent: 'space-between',
            gap: 2,
            px: pad,
            pt: pad,
            pb: 0,
            ...headerSx,
          }}
        >
          <Box sx={{ minWidth: 0 }}>
            {title && (
              <Typography variant="h6" sx={{ color: 'text.primary' }}>
                {title}
              </Typography>
            )}
            {subtitle && (
              <Typography variant="body2" sx={{ mt: 0.5, color: 'text.secondary' }}>
                {subtitle}
              </Typography>
            )}
          </Box>
          {actions && <Box sx={{ flexShrink: 0 }}>{actions}</Box>}
        </Box>
      )}

      <CardContent
        sx={{
          p: pad,
          ...(hasHeader && { pt: pad ? 2 : 0 }),
          '&:last-child': { pb: pad },
          ...contentSx,
        }}
      >
        {children}
      </CardContent>

      {footer && <Box sx={{ px: pad, pb: pad }}>{footer}</Box>}
    </>
  );

  return (
    <Card
      variant={variant === 'elevated' ? 'elevation' : 'outlined'}
      elevation={variant === 'elevated' ? 1 : 0}
      sx={{
        borderRadius: 3,
        ...(fullHeight && { height: '100%' }),
        ...(variant === 'plain' && { border: 'none', boxShadow: 'none', bgcolor: 'transparent' }),
        ...(variant === 'outlined' && { borderColor: 'divider' }),
        ...sx,
      }}
    >
      {onClick ? <CardActionArea onClick={onClick}>{body}</CardActionArea> : body}
    </Card>
  );
};

export default BudCard;
