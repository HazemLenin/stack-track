import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-not-found-page',
  imports: [RouterLink],
  template: '<h1>Page not found</h1><a routerLink="/">Back home</a>',
})
export class NotFoundPage {}
