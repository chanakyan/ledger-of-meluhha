module audit_iron_db

abstract sig StateID {}
one sig St1, St2, St3, St4, St5, St6, St7, St8, St9, St10 extends StateID {}

abstract sig InstID {}
one sig In1, In2, In3, In4, In5, In6, In9 extends InstID {}

abstract sig MethodID {}
one sig Me1, Me2, Me3, Me4, Me5 extends MethodID {}

abstract sig MatID {}
one sig Ma1, Ma2, Ma3, Ma4, Ma5 extends MatID {}

abstract sig SiteID {}
one sig Si1, Si2, Si3, Si4, Si5, Si6, Si7, Si101, Si102, Si103, Si104, Si105, Si106, Si107, Si108, Si109, Si110, Si111, Si112, Si113, Si114, Si115, Si116, Si117, Si118, Si119, Si120, Si121, Si122, Si123, Si124, Si125, Si126, Si127 extends SiteID {}

abstract sig SampID {}
one sig Sa1, Sa2, Sa3, Sa4, Sa5, Sa6, Sa7, Sa8, Sa9, Sa10, Sa11, Sa12, Sa20, Sa21, Sa22, Sa23, Sa24, Sa30, Sa31, Sa40, Sa50, Sa60, Sa61, Sa62, Sa70, Sa101, Sa102, Sa103, Sa104, Sa105, Sa106, Sa107, Sa108, Sa109, Sa110, Sa111, Sa112, Sa113, Sa114, Sa115, Sa116, Sa117, Sa118, Sa119, Sa120, Sa121, Sa122, Sa123, Sa124, Sa125, Sa126, Sa127 extends SampID {}

abstract sig MeasID {}
one sig Mx1, Mx2, Mx3, Mx4, Mx5, Mx6, Mx7, Mx8, Mx9, Mx10, Mx11, Mx12, Mx20, Mx21, Mx22, Mx23, Mx24, Mx30, Mx31, Mx40, Mx50, Mx60, Mx61, Mx62, Mx70, Mx101, Mx102, Mx103, Mx104, Mx105, Mx106, Mx107, Mx108, Mx109, Mx110, Mx111, Mx112, Mx113, Mx114, Mx115, Mx116, Mx117, Mx118, Mx119, Mx120, Mx121, Mx122, Mx123, Mx124, Mx125, Mx126, Mx127 extends MeasID {}

one sig DB {
    site_state:  SiteID -> one StateID,
    samp_site:   SampID -> one SiteID,
    samp_mat:    SampID -> one MatID,
    meas_samp:   MeasID -> one SampID,
    meas_inst:   MeasID -> one InstID,
    meas_method: MeasID -> one MethodID
}

fact F_site_state {
    DB.site_state = (
        Si1->St1 +
        Si2->St1 +
        Si3->St1 +
        Si4->St1 +
        Si5->St1 +
        Si6->St1 +
        Si7->St1 +
        Si101->St2 +
        Si102->St3 +
        Si103->St4 +
        Si104->St2 +
        Si105->St2 +
        Si106->St5 +
        Si107->St5 +
        Si108->St2 +
        Si109->St6 +
        Si110->St7 +
        Si111->St8 +
        Si112->St5 +
        Si113->St2 +
        Si114->St5 +
        Si115->St5 +
        Si116->St2 +
        Si117->St9 +
        Si118->St10 +
        Si119->St5 +
        Si120->St4 +
        Si121->St10 +
        Si122->St2 +
        Si123->St2 +
        Si124->St7 +
        Si125->St5 +
        Si126->St7 +
        Si127->St5
    )
}

fact F_samp_site {
    DB.samp_site = (
        Sa1->Si1 +
        Sa2->Si1 +
        Sa3->Si1 +
        Sa4->Si1 +
        Sa5->Si1 +
        Sa6->Si1 +
        Sa7->Si1 +
        Sa8->Si1 +
        Sa9->Si1 +
        Sa10->Si1 +
        Sa11->Si1 +
        Sa12->Si1 +
        Sa20->Si2 +
        Sa21->Si2 +
        Sa22->Si2 +
        Sa23->Si2 +
        Sa24->Si2 +
        Sa30->Si3 +
        Sa31->Si3 +
        Sa40->Si4 +
        Sa50->Si5 +
        Sa60->Si6 +
        Sa61->Si6 +
        Sa62->Si6 +
        Sa70->Si7 +
        Sa101->Si101 +
        Sa102->Si102 +
        Sa103->Si103 +
        Sa104->Si104 +
        Sa105->Si105 +
        Sa106->Si106 +
        Sa107->Si107 +
        Sa108->Si108 +
        Sa109->Si109 +
        Sa110->Si110 +
        Sa111->Si111 +
        Sa112->Si112 +
        Sa113->Si113 +
        Sa114->Si114 +
        Sa115->Si115 +
        Sa116->Si116 +
        Sa117->Si117 +
        Sa118->Si118 +
        Sa119->Si119 +
        Sa120->Si120 +
        Sa121->Si121 +
        Sa122->Si122 +
        Sa123->Si123 +
        Sa124->Si124 +
        Sa125->Si125 +
        Sa126->Si126 +
        Sa127->Si127
    )
}

fact F_samp_mat {
    DB.samp_mat = (
        Sa1->Ma1 +
        Sa2->Ma1 +
        Sa3->Ma1 +
        Sa4->Ma4 +
        Sa5->Ma4 +
        Sa6->Ma2 +
        Sa7->Ma4 +
        Sa8->Ma4 +
        Sa9->Ma4 +
        Sa10->Ma4 +
        Sa11->Ma4 +
        Sa12->Ma1 +
        Sa20->Ma1 +
        Sa21->Ma1 +
        Sa22->Ma3 +
        Sa23->Ma2 +
        Sa24->Ma2 +
        Sa30->Ma1 +
        Sa31->Ma1 +
        Sa40->Ma1 +
        Sa50->Ma5 +
        Sa60->Ma5 +
        Sa61->Ma5 +
        Sa62->Ma5 +
        Sa70->Ma1 +
        Sa101->Ma1 +
        Sa102->Ma1 +
        Sa103->Ma1 +
        Sa104->Ma1 +
        Sa105->Ma1 +
        Sa106->Ma1 +
        Sa107->Ma1 +
        Sa108->Ma1 +
        Sa109->Ma1 +
        Sa110->Ma4 +
        Sa111->Ma1 +
        Sa112->Ma1 +
        Sa113->Ma1 +
        Sa114->Ma1 +
        Sa115->Ma4 +
        Sa116->Ma1 +
        Sa117->Ma1 +
        Sa118->Ma1 +
        Sa119->Ma1 +
        Sa120->Ma1 +
        Sa121->Ma1 +
        Sa122->Ma1 +
        Sa123->Ma1 +
        Sa124->Ma1 +
        Sa125->Ma1 +
        Sa126->Ma1 +
        Sa127->Ma1
    )
}

fact F_meas_samp {
    DB.meas_samp = (
        Mx1->Sa1 +
        Mx2->Sa2 +
        Mx3->Sa3 +
        Mx4->Sa4 +
        Mx5->Sa5 +
        Mx6->Sa6 +
        Mx7->Sa7 +
        Mx8->Sa8 +
        Mx9->Sa9 +
        Mx10->Sa10 +
        Mx11->Sa11 +
        Mx12->Sa12 +
        Mx20->Sa20 +
        Mx21->Sa21 +
        Mx22->Sa22 +
        Mx23->Sa23 +
        Mx24->Sa24 +
        Mx30->Sa30 +
        Mx31->Sa31 +
        Mx40->Sa40 +
        Mx50->Sa50 +
        Mx60->Sa60 +
        Mx61->Sa61 +
        Mx62->Sa62 +
        Mx70->Sa70 +
        Mx101->Sa101 +
        Mx102->Sa102 +
        Mx103->Sa103 +
        Mx104->Sa104 +
        Mx105->Sa105 +
        Mx106->Sa106 +
        Mx107->Sa107 +
        Mx108->Sa108 +
        Mx109->Sa109 +
        Mx110->Sa110 +
        Mx111->Sa111 +
        Mx112->Sa112 +
        Mx113->Sa113 +
        Mx114->Sa114 +
        Mx115->Sa115 +
        Mx116->Sa116 +
        Mx117->Sa117 +
        Mx118->Sa118 +
        Mx119->Sa119 +
        Mx120->Sa120 +
        Mx121->Sa121 +
        Mx122->Sa122 +
        Mx123->Sa123 +
        Mx124->Sa124 +
        Mx125->Sa125 +
        Mx126->Sa126 +
        Mx127->Sa127
    )
}

fact F_meas_inst {
    DB.meas_inst = (
        Mx1->In1 +
        Mx2->In1 +
        Mx3->In1 +
        Mx4->In4 +
        Mx5->In3 +
        Mx6->In1 +
        Mx7->In4 +
        Mx8->In4 +
        Mx9->In3 +
        Mx10->In4 +
        Mx11->In4 +
        Mx12->In1 +
        Mx20->In1 +
        Mx21->In1 +
        Mx22->In5 +
        Mx23->In5 +
        Mx24->In5 +
        Mx30->In1 +
        Mx31->In1 +
        Mx40->In1 +
        Mx50->In2 +
        Mx60->In2 +
        Mx61->In2 +
        Mx62->In2 +
        Mx70->In6 +
        Mx101->In9 +
        Mx102->In9 +
        Mx103->In9 +
        Mx104->In9 +
        Mx105->In9 +
        Mx106->In9 +
        Mx107->In9 +
        Mx108->In9 +
        Mx109->In9 +
        Mx110->In9 +
        Mx111->In9 +
        Mx112->In9 +
        Mx113->In9 +
        Mx114->In9 +
        Mx115->In9 +
        Mx116->In9 +
        Mx117->In9 +
        Mx118->In9 +
        Mx119->In9 +
        Mx120->In9 +
        Mx121->In9 +
        Mx122->In9 +
        Mx123->In9 +
        Mx124->In9 +
        Mx125->In9 +
        Mx126->In9 +
        Mx127->In9
    )
}

fact F_meas_method {
    DB.meas_method = (
        Mx1->Me1 +
        Mx2->Me1 +
        Mx3->Me1 +
        Mx4->Me2 +
        Mx5->Me2 +
        Mx6->Me1 +
        Mx7->Me2 +
        Mx8->Me2 +
        Mx9->Me2 +
        Mx10->Me2 +
        Mx11->Me2 +
        Mx12->Me1 +
        Mx20->Me1 +
        Mx21->Me1 +
        Mx22->Me1 +
        Mx23->Me1 +
        Mx24->Me1 +
        Mx30->Me1 +
        Mx31->Me1 +
        Mx40->Me1 +
        Mx50->Me1 +
        Mx60->Me1 +
        Mx61->Me1 +
        Mx62->Me1 +
        Mx70->Me1 +
        Mx101->Me3 +
        Mx102->Me3 +
        Mx103->Me3 +
        Mx104->Me3 +
        Mx105->Me3 +
        Mx106->Me1 +
        Mx107->Me1 +
        Mx108->Me3 +
        Mx109->Me3 +
        Mx110->Me5 +
        Mx111->Me3 +
        Mx112->Me3 +
        Mx113->Me3 +
        Mx114->Me1 +
        Mx115->Me4 +
        Mx116->Me3 +
        Mx117->Me3 +
        Mx118->Me3 +
        Mx119->Me1 +
        Mx120->Me3 +
        Mx121->Me3 +
        Mx122->Me1 +
        Mx123->Me3 +
        Mx124->Me3 +
        Mx125->Me3 +
        Mx126->Me3 +
        Mx127->Me3
    )
}

fun organic_mats   : set MatID    { Ma1 + Ma2 + Ma3 + Ma5 }
fun lumin_mats_set : set MatID    { Ma4 }
fun rc_methods     : set MethodID { Me1 + Me3 }
fun lm_methods     : set MethodID { Me2 + Me4 + Me5 }

assert A1_SiteState_Range  { DB.site_state[SiteID] in StateID }
check A1_SiteState_Range for 60

assert A2_SampSite_Range   { DB.samp_site[SampID] in SiteID }
check A2_SampSite_Range for 60

assert A3_MeasSamp_Range   { DB.meas_samp[MeasID] in SampID }
check A3_MeasSamp_Range for 60

assert A4_MeasInst_Range   { DB.meas_inst[MeasID] in InstID }
check A4_MeasInst_Range for 60

assert A5_MeasMethod_Range { DB.meas_method[MeasID] in MethodID }
check A5_MeasMethod_Range for 60

assert A6_SampMat_Range    { DB.samp_mat[SampID] in MatID }
check A6_SampMat_Range for 60

assert A7_MeasOneSite { all mx: MeasID | one DB.site_state[DB.samp_site[DB.meas_samp[mx]]] }
check A7_MeasOneSite for 60

assert A8_Radiocarbon_Organic {
    all mx: MeasID | DB.meas_method[mx] in rc_methods => DB.samp_mat[DB.meas_samp[mx]] in organic_mats }
check A8_Radiocarbon_Organic for 60

assert A9_Lumin_Material {
    all mx: MeasID | DB.meas_method[mx] in lm_methods => DB.samp_mat[DB.meas_samp[mx]] in lumin_mats_set }
check A9_Lumin_Material for 60

assert A10_No_Orphan_Inst { InstID in DB.meas_inst[MeasID] }
check A10_No_Orphan_Inst for 60

assert A11_No_Orphan_Mat  { MatID in DB.samp_mat[SampID] }
check A11_No_Orphan_Mat for 60

assert A12_No_Dup_Measurement {
    all disj m1, m2: MeasID |
        DB.meas_samp[m1] = DB.meas_samp[m2] and DB.meas_inst[m1] = DB.meas_inst[m2] and
        DB.meas_method[m1] = DB.meas_method[m2] => m1 = m2 }
check A12_No_Dup_Measurement for 60

assert A13_CrossLab_Distinct {
    all sa: SampID | let ms = DB.meas_samp.sa | #ms > 1 => #(DB.meas_inst[ms]) > 1 }
check A13_CrossLab_Distinct for 60

assert A14_No_Orphan_Site  { SiteID in DB.samp_site[SampID] }
check A14_No_Orphan_Site for 60

assert A15_SiteState_Total { all si: SiteID | one DB.site_state[si] }
check A15_SiteState_Total for 60