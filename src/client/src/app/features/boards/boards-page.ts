import { Component } from '@angular/core';

@Component({
  selector: 'app-boards-page',
  template: `
    <h1>My boards</h1>
    <div class="empty">
      <strong>No boards yet</strong>
      <span>Boards you create will show up here.</span>
    </div>
  `,
  styles: `
    h1 {
      margin: 0 0 16px;
      font: var(--mat-sys-headline-medium);
      letter-spacing: -0.02em;
    }

    .empty {
      display: grid;
      justify-items: center;
      gap: 4px;
      padding: 36px 20px;
      color: var(--mat-sys-on-surface-variant);
      border: 1.5px dashed var(--mat-sys-outline-variant);
      border-radius: 14px;

      strong {
        color: var(--mat-sys-on-surface);
        font: var(--mat-sys-title-medium);
      }
    }
  `,
})
export class BoardsPage {}
