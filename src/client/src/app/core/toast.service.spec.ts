import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ToastService } from './toast.service';

describe('ToastService', () => {
  it('dismisses the open toast before showing the next', () => {
    const calls: string[] = [];
    const opens: unknown[][] = [];
    const snack = {
      dismiss: () => calls.push('dismiss'),
      open: (...args: unknown[]) => {
        calls.push('open');
        opens.push(args);
      },
    };
    TestBed.configureTestingModule({ providers: [{ provide: MatSnackBar, useValue: snack }] });
    const toast = TestBed.inject(ToastService);

    toast.show('a');
    toast.show('b');

    expect(calls).toEqual(['dismiss', 'open', 'dismiss', 'open']);
    expect(opens[1]).toEqual(['b', 'Dismiss', { duration: 6000 }]);
  });
});
