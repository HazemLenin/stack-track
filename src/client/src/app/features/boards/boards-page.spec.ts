import { TestBed } from '@angular/core/testing';
import { BoardsPage } from './boards-page';

describe('BoardsPage', () => {
  it('shows the My boards heading and an empty state', async () => {
    const fixture = TestBed.createComponent(BoardsPage);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('h1')?.textContent).toBe('My boards');
    expect(el.querySelector('.empty')?.textContent).toContain('No boards yet');
  });
});
