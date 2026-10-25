import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { PageEvent, MatPaginatorModule } from '@angular/material/paginator';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { Observable } from 'rxjs';
import { AuthService } from '../../auth/auth.service';
import { AdminUser, AdminUsersService, ListUsersParams, UserPage, UserRole } from './admin-users.service';

@Component({
  selector: 'app-confirm-deactivate-dialog',
  imports: [MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Deactivate user?</h2>
    <mat-dialog-content>They will no longer be able to sign in. Their boards, cards and comments are kept.</mat-dialog-content>
    <mat-dialog-actions>
      <button matButton mat-dialog-close>Cancel</button>
      <button matButton="filled" [mat-dialog-close]="true">Deactivate</button>
    </mat-dialog-actions>
  `,
})
export class ConfirmDeactivateDialog {}

@Component({
  selector: 'app-admin-users-page',
  imports: [DatePipe, MatTableModule, MatSortModule, MatPaginatorModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './admin-users-page.html',
  styleUrl: './admin-users-page.scss',
})
export class AdminUsersPage {
  private readonly api = inject(AdminUsersService);
  private readonly dialog = inject(MatDialog);
  private readonly auth = inject(AuthService);

  protected readonly columns = ['name', 'email', 'role', 'status', 'created', 'actions'];
  protected readonly data = signal<UserPage>({ items: [], page: 1, pageSize: 20, totalCount: 0 });

  private query: ListUsersParams = { search: '', sort: 'name', dir: 'asc', page: 1 };
  private debounce?: ReturnType<typeof setTimeout>;

  constructor() {
    this.load();
  }

  protected isOwn(u: AdminUser): boolean {
    return u.id === this.auth.user()?.id;
  }

  protected onSearch(value: string): void {
    clearTimeout(this.debounce);
    this.debounce = setTimeout(() => this.reload({ search: value.trim(), page: 1 }), 300);
  }

  protected onSort(s: Sort): void {
    // A cleared header falls back to the default order.
    const sort = s.direction ? (s.active as 'name' | 'created') : 'name';
    this.reload({ sort, dir: s.direction || 'asc', page: 1 });
  }

  protected onPage(e: PageEvent): void {
    this.reload({ page: e.pageIndex + 1 });
  }

  protected deactivate(u: AdminUser): void {
    this.dialog
      .open(ConfirmDeactivateDialog)
      .afterClosed()
      .subscribe((ok) => ok && this.act(this.api.deactivate(u.id)));
  }

  protected reactivate(u: AdminUser): void {
    this.act(this.api.reactivate(u.id));
  }

  protected changeRole(u: AdminUser, role: string): void {
    this.act(this.api.changeRole(u.id, role as UserRole));
  }

  private reload(patch: Partial<ListUsersParams>): void {
    this.query = { ...this.query, ...patch };
    this.load();
  }

  private load(): void {
    this.api.list(this.query).subscribe({ next: (d) => this.data.set(d), error: () => undefined });
  }

  // The error interceptor already toasts failures; reload either way so the table shows the truth.
  private act(call: Observable<void>): void {
    call.subscribe({ next: () => this.load(), error: () => this.load() });
  }
}
