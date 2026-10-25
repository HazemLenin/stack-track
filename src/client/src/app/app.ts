import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterOutlet } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { MatToolbarModule } from '@angular/material/toolbar';
import { AuthService } from './features/auth/auth.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, MatToolbarModule, MatButtonModule, MatMenuModule],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly user = this.auth.user;

  protected signOut(): void {
    // The session is cleared whether or not the request succeeds.
    this.auth.signOut().subscribe({
      complete: () => this.router.navigateByUrl('/auth/sign-in'),
      error: () => this.router.navigateByUrl('/auth/sign-in'),
    });
  }
}
