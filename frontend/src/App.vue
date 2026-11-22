<script setup lang="ts">
import { RouterLink, RouterView } from 'vue-router'
</script>

<template>
  <header class="topbar">
    <RouterLink class="brand" to="/">
      <span class="brand-mark" aria-hidden="true">▤</span>
      Invoice Desk
    </RouterLink>
    <nav aria-label="Main navigation">
      <RouterLink to="/">Cases</RouterLink>
    </nav>
    <span class="demo-role">Accounts payable · Demo</span>
  </header>
  <main class="page">
    <RouterView :key="$route.path" />
  </main>
</template>
