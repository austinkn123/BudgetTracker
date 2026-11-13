import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import Skeleton from './Skeleton';

describe('Skeleton', () => {
  it('is hidden from assistive technology', () => {
    const { container } = render(<Skeleton />);

    const skeleton = container.firstElementChild as HTMLElement;
    expect(skeleton).toHaveAttribute('aria-hidden', 'true');
    expect(skeleton).toBeEmptyDOMElement();
  });

  it('applies the requested dimensions, as numbers or CSS strings', () => {
    const { container } = render(<Skeleton variant="rectangular" width={120} height="2rem" />);

    const skeleton = container.firstElementChild as HTMLElement;
    expect(skeleton.style.width).toBe('120px');
    expect(skeleton.style.height).toBe('2rem');
  });

  it('rounds fully for the circular variant', () => {
    const { container } = render(<Skeleton variant="circular" width={32} height={32} />);

    expect(container.firstElementChild).toHaveClass('rounded-full');
  });
});
