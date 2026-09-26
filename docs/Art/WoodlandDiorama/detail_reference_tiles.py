"""Eight landing stones derived from the approved trial shape."""
for index in range(len(poses)):
    for old in list(scene.objects):
        if old.name.split('.')[0]=='TrailCell_%02d'%index:remove(old)
    x,y=poses[index]
    before=poses[max(0,index-1)];after=poses[min(len(poses)-1,index+1)]
    angle=math.atan2(after[1]-before[1],after[0]-before[0])
    n=16;verts=[];faces=[]
    # Rounded-square outline with a low sidewall and deliberately uneven chamfers.
    for ring,(rx,ry,z) in enumerate([(.400,.380,.025),(.433,.408,.085),(.426,.400,.19),(.395,.371,.226)]):
        for j in range(n):
            a=(j+.5)*math.tau/n
            sx=math.copysign(abs(math.cos(a))**.29,math.cos(a))
            sy=math.copysign(abs(math.sin(a))**.29,math.sin(a))
            wobble=1+.018*math.sin(j*5+index*3)+.012*math.cos(j*7-index)
            verts.append((sx*rx*wobble,sy*ry*wobble,z+(.12 if index==6 else 0)+.005*math.sin(j*3+index)))
    for ring in range(3):
        for j in range(n):
            a=ring*n+j;b=ring*n+(j+1)%n
            faces.append((a,b,b+n,a+n))
    faces.append(tuple(range(3*n,4*n)))
    o=mesh('TrailCell_%02d'%index,verts,faces,'TrialStone%d'%(index%2))
    o.location=(x,y,0);o.rotation_euler.z=angle
    bevel(o,.012)
    # Small chips and moss at the feet. Leave the landing face open.
    for side in [-1,1]:
        a=angle+side*math.pi*.63
        px=x+math.cos(a)*.39;py=y+math.sin(a)*.36
        for j in range(4):
            ball('Tile foot moss',(px+math.sin(j*2.1+index)*.045,py+math.cos(j*2.3)*.045,.04+(.12 if index==6 else 0)),(.040,.027,.021),'ShoreMoss')
        if side==1:
            rock('Tile setting pebble',(px+math.cos(a)*.07,py+math.sin(a)*.05,.04),(.055,.044,.028),'HeroRock')
