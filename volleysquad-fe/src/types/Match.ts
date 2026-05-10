export interface Match {
  id: string;
  playDate: string;  // ISO datetime
  location: string;
  maxSlots: number;  // default 18
  registeredMemberIds: string[];
}

export interface Team {
  teamName: string;
  members: TeamMember[];
  totalSkillPoint: number;
}

export interface TeamMember {
  id: string;
  name: string;
  skillPoint: number;
  position: string;
}